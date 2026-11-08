using System.Text;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Buffers genuine API results at the actual TLS destination; faults change only the response after owned API work has completed.</summary>
internal sealed class GrantTransportProbe : IStartupFilter
{
    internal const string Canary = "grant-transport-private-canary";
    internal const string PrivateHeader = "X-Private";
    internal const string CookieHeader = "Set-Cookie";
    internal const string Redirect = "/grant-transport-redirect";
    private const string CookieSuffix = "=upstream; Secure";
    private const string Bearer = "Bearer ";
    private const string Json = "application/json";
    private const string KeyHeader = "Idempotency-Key";
    private static readonly string[] ForbiddenHeaders = ["Cookie", "X-CSRF-TOKEN", "X-Identity"];
    private string access = string.Empty;
    internal int? SuccessStatus
    {
        get;
        set;
    }
    internal string Path { get; set; } = string.Empty;
    internal string? Method
    {
        get; set;
    }
    internal string? Body
    {
        get; set;
    }
    internal string? Version
    {
        get; set;
    }
    internal string? Key
    {
        get; set;
    }
    internal string Fault { get; set; } = string.Empty;
    internal int Requests
    {
        get; private set;
    }
    internal int Responses
    {
        get; private set;
    }
    internal int Redirects
    {
        get; private set;
    }
    internal string Original { get; private set; } = string.Empty;
    internal string WireBody { get; private set; } = string.Empty;
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Canceled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Observes the genuine protected server credential only in memory, without exposing it to browser requests or assertion output.</summary>
    internal async Task BindAsync(ApplicationHttpFixture test)
    {
        await using var scope = test.Environment.Client.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>().Set<AdminSessionTicket>().AsNoTracking()
            .SingleAsync(value => value.RevokedAt == null, TestContext.Current.CancellationToken);
        var ticket = scope.ServiceProvider.GetRequiredService<SessionTicketCodec>().Unprotect(row)!;
        access = BrowserAccessCredential.Read(ticket.Properties, test.Now)!.Value;
    }

    /// <summary>Installs response observation without replacing routes, native authentication or API persistence.</summary>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(InvokeAsync);
        next(app);
    };

    /// <summary>Observes exact forwarding and genuine successful output, then applies a single bounded response fault.</summary>
    private async Task InvokeAsync(HttpContext http, RequestDelegate next)
    {
        if (http.Request.Path == Redirect)
        {
            Redirects++;
            http.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        if (http.Request.Path != Path)
        {
            await next(http);
            return;
        }
        Requests++;
        await RequireRequestAsync(http);
        var output = http.Response.Body;
        using var buffer = new MemoryStream();
        http.Response.Body = buffer;
        try
        {
            await next(http);
        }
        finally { http.Response.Body = output; }
        Assert.Equal(SuccessStatus ?? (Body is null ? StatusCodes.Status200OK : StatusCodes.Status202Accepted), http.Response.StatusCode);
        Assert.False(http.Response.HasStarted);
        Responses++;
        Original = Encoding.UTF8.GetString(buffer.ToArray());
        try
        {
            await PublishAsync(http);
        }
        finally { Completed.TrySetResult(); }
    }

    /// <summary>Requires one exact server bearer and immutable intent; browser credentials and arbitrary headers never reach the API.</summary>
    private async Task RequireRequestAsync(HttpContext http)
    {
        Assert.True(http.Request.Headers.Authorization.ToString() == Bearer + access);
        Assert.All(ForbiddenHeaders, name => Assert.False(http.Request.Headers.ContainsKey(name)));
        Assert.Equal(Method ?? (Body is null ? HttpMethods.Get : HttpMethods.Post), http.Request.Method);
        Assert.Equal(string.Empty, http.Request.QueryString.Value ?? string.Empty);
        Assert.Equal(Version ?? string.Empty, http.Request.Headers.IfMatch.ToString());
        Assert.Equal(Key ?? string.Empty, http.Request.Headers[KeyHeader].ToString());
        http.Request.EnableBuffering();
        using var input = new MemoryStream();
        await http.Request.Body.CopyToAsync(input, http.RequestAborted);
        Assert.Equal(Body ?? string.Empty, Encoding.UTF8.GetString(input.ToArray()));
        if (Body is not null)
        {
            Assert.Equal(Json, http.Request.ContentType);
            http.Request.Body.Position = 0;
        }
    }

    /// <summary>Joins the actual upstream cancellation and allows a held valid response to cross a controlled session boundary.</summary>
    private async Task PublishAsync(HttpContext http)
    {
        if (Fault == GrantTransportFaults.Hold)
        {
            Entered.TrySetResult();
            try
            {
                await Release.Task.WaitAsync(http.RequestAborted);
            }
            catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
            {
                Canceled.TrySetResult();
                return;
            }
        }
        if (Fault == GrantTransportFaults.Abort)
        {
            http.Abort();
            return;
        }
        http.Response.Headers[CookieHeader] = Canary + CookieSuffix;
        http.Response.Headers[PrivateHeader] = Canary;
        var body = GrantTransportFaults.Apply(http, Original, Fault);
        WireBody = body;
        http.Response.ContentLength = Fault == GrantTransportFaults.StreamOverflow ? null : Encoding.UTF8.GetByteCount(body);
        await http.Response.WriteAsync(body, http.RequestAborted);
    }
}
