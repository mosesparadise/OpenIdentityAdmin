using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Client;
using OpenIddict.Client.AspNetCore;

namespace IdentityPlatform.Testing;

/// <summary>Runs assertion-free native ASP.NET requests through the production storage/response boundary for both in-process and OS-process fixtures.</summary>
internal static class NativeClientRequest
{
    private const string ChallengePath = "/start";
    private const string CookieSeparator = "; ";
    /// <summary>Runs a real named native handler under the production response/storage boundary and completes the original host callbacks.</summary>
    internal static async Task<NativeClientHttpResult> InvokeAsync(IServiceProvider services, Uri clientUri, Uri? callback, string? cookie, CancellationToken cancellation)
    {
        await using var initial = services.CreateAsyncScope();
        var uri = callback ?? new Uri(clientUri, ChallengePath);
        var lifetime = new NativeRequestLifetime(cancellation);
        var http = new DefaultHttpContext { RequestServices = initial.ServiceProvider };
        http.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
        http.Request.Scheme = uri.Scheme;
        http.Request.Host = new HostString(uri.Host, uri.Port);
        http.Request.Path = uri.AbsolutePath;
        http.Request.QueryString = new QueryString(uri.Query);
        http.Request.Method = HttpMethods.Get;
        if (cookie is not null)
        {
            http.Request.Headers.Cookie = cookie;
        }
        var completion = new NativeHttpCompletion();
        http.Features.Set<IHttpResponseFeature>(completion);
        await using var output = new MemoryStream();
        http.Response.Body = output;
        var result = new NativeClientHttpResult();
        try
        {
            await ProtocolHttpBoundary.RunAsync(http, services.GetRequiredService<IServiceScopeFactory>(),
                services.GetRequiredService<TimeProvider>(), async (context, _, _) =>
                {
                    try
                    {
                        await DispatchAsync(context, callback is not null, result);
                    }
                    catch (Exception error) { result.Failure = error; throw; }
                    return ProtocolResponseOutcome.Committed;
                });
        }
        finally { await completion.CompleteAsync(); }
        result.Status = http.Response.StatusCode;
        result.Location = http.Response.Headers.Location.ToString();
        result.Cookies = http.Response.Headers.SetCookie.Where(value => value is not null).Select(value => value!).ToArray();
        result.Cookie = string.Join(CookieSeparator, result.Cookies.Select(value => value.Split(';')[0]));
        result.BodyLength = output.Length;
        result.Aborted = lifetime.Aborted;
        return result;
    }

    /// <summary>Uses native challenge, callback parsing/correlation and authentication results; no principal or context is fabricated.</summary>
    private static async Task DispatchAsync(HttpContext http, bool callback, NativeClientHttpResult result)
    {
        var handler = ActivatorUtilities.CreateInstance<OpenIddictClientAspNetCoreHandler>(http.RequestServices);
        await handler.InitializeAsync(new AuthenticationScheme(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme, null, handler.GetType()), http);
        var handled = await handler.HandleRequestAsync();
        result.Rejection = http.Features.Get<OpenIddictClientAspNetCoreFeature>()?.Transaction?
            .GetProperty<string>(NativeClientRejection.Property) ?? NativeClientRejection.None;
        if (callback)
        {
            if (!handled)
            {
                result.Authentication = await handler.AuthenticateAsync();
            }
        }
        else
        {
            if (handled)
            {
                throw new InvalidOperationException();
            }
            await handler.ChallengeAsync(new AuthenticationProperties());
        }
    }

}

/// <summary>Keeps synthetic authentication material in memory without credential-bearing record formatting.</summary>
internal sealed class NativeClientHttpResult
{
    internal bool Aborted
    {
        get; set;
    }
    internal string Rejection { get; set; } = NativeClientRejection.None;
    internal int Status
    {
        get; set;
    }
    internal long BodyLength
    {
        get; set;
    }
    internal string Location { get; set; } = string.Empty;
    internal string Cookie { get; set; } = string.Empty;
    internal string[] Cookies { get; set; } = [];
    internal AuthenticateResult? Authentication
    {
        get; set;
    }
    internal Exception? Failure
    {
        get; set;
    }
}
