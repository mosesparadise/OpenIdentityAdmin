using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Drives actual native HTTP and shared production profile dispatch inside the production hosting/ownership boundary.</summary>
internal static class NativeAccountHttp
{
    private const string CookieHeader = "Set-Cookie";

    private static readonly object ContextKey = new();
    private const string FormMediaType = "application/x-www-form-urlencoded";

    /// <summary>Retains the actual outer context solely for the PAR publication check; it supplies no authentication result.</summary>
    internal static ValueTask Remember(OpenIddictServerEvents.ProcessRequestContext context)
    {
        context.Transaction.GetHttpRequest()!.HttpContext.Items.Add(ContextKey, context);
        return ValueTask.CompletedTask;
    }

    /// <summary>Supplies real form/query/cookie bytes and runs host completion even if the owned operation fails before returning.</summary>
    internal static async Task<NativeAccountResult> InvokeAsync(AdministratorProtocolScenario scenario, string path,
        Dictionary<string, string> fields, bool cookie = false, CancellationToken? cancellation = null)
    {
        var token = cancellation ?? TestContext.Current.CancellationToken;
        using var form = new FormUrlEncodedContent(fields);
        await using var input = await form.ReadAsStreamAsync(token);
        await using var output = new MemoryStream();
        await using var requestScope = scenario.Services.CreateAsyncScope();
        var http = Create(scenario, path, fields, input, cookie, token);
        http.RequestServices = requestScope.ServiceProvider;
        var completion = new NativeHttpCompletion();
        http.Features.Set<IHttpResponseFeature>(completion);
        http.Response.Body = output;
        var result = new NativeAccountResult();
        try
        {
            await ProtocolHosting.InvokeAsync(http, async context =>
            {
                try
                {
                    await DispatchAsync(context, path, result);
                }
                catch (Exception error) { result.Failure = error; throw; }
            });
            result.Claimed = http.Response.StatusCode is StatusCodes.Status200OK or StatusCodes.Status201Created or StatusCodes.Status302Found;
        }
        finally { await completion.CompleteAsync(); }
        result.Status = http.Response.StatusCode;
        result.Body = output.ToArray();
        result.ContentType = http.Response.ContentType;
        result.Location = http.Response.Headers.Location.ToString();
        result.HasCookie = http.Response.Headers.ContainsKey(CookieHeader);
        return result;
    }

    /// <summary>Uses HTTPS and the exact configured issuer; only authorization receives the real administrator cookie.</summary>
    private static DefaultHttpContext Create(AdministratorProtocolScenario scenario, string path, Dictionary<string, string> fields, Stream body, bool cookie,
        CancellationToken cancellation)
    {
        var http = new DefaultHttpContext { RequestServices = scenario.Services, RequestAborted = cancellation };
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.Host = new HostString(scenario.Issuer.Host, scenario.Issuer.Port);
        http.Request.Path = path;
        http.Request.Method = path is AdministratorProtocolScenario.ParPath or AdministratorProtocolScenario.TokenPath ? HttpMethods.Post : HttpMethods.Get;
        if (HttpMethods.IsGet(http.Request.Method))
        {
            http.Request.QueryString = QueryString.Create(fields.Select(pair => KeyValuePair.Create<string, string?>(pair.Key, pair.Value)));
        }
        else
        {
            http.Request.ContentType = FormMediaType;
            http.Request.ContentLength = body.Length;
            http.Request.Body = body;
        }
        if (cookie)
        {
            http.Request.Headers.Cookie = scenario.Cookie;
        }
        return http;
    }

    /// <summary>Executes native authentication/passthrough and actual SignIn results; handled/skipped return alone never grants publication.</summary>
    internal static async Task DispatchAsync(HttpContext http, string path, NativeAccountResult result)
    {
        var handler = ActivatorUtilities.CreateInstance<OpenIddictServerAspNetCoreHandler>(http.RequestServices);
        await handler.InitializeAsync(new AuthenticationScheme(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, null, handler.GetType()), http);
        var handled = await handler.HandleRequestAsync();
        var outer = (OpenIddictServerEvents.ProcessRequestContext)http.Items[ContextKey]!;
        result.Outcome = outer.IsRejected ? NativeAccountOutcome.Rejected : handled ? NativeAccountOutcome.Handled : NativeAccountOutcome.Passthrough;
        if (path is AdministratorProtocolScenario.AuthorizePath or AdministratorProtocolScenario.TokenPath && !handled)
        {
            Assert.False(handled);
            Assert.True(outer.IsRequestSkipped);
            var decision = path == AdministratorProtocolScenario.AuthorizePath
                ? await ProtocolDecisions.AuthorizeAsync(http) : await ProtocolDecisions.ExchangeAsync(http);
            await decision.ExecuteAsync(http);
        }

    }
}

/// <summary>Retains synthetic response material in memory without a generated credential-bearing ToString implementation.</summary>
internal sealed class NativeAccountResult
{
    internal string? ContentType
    {
        get; set;
    }
    internal int Status
    {
        get; set;
    }
    internal byte[] Body { get; set; } = [];
    internal string Location { get; set; } = string.Empty;
    internal bool HasCookie
    {
        get; set;
    }
    internal bool Claimed
    {
        get; set;
    }
    internal Exception? Failure
    {
        get; set;
    }
    internal NativeAccountOutcome Outcome
    {
        get; set;
    }
}

/// <summary>Provides bounded native control-flow diagnostics without error descriptions or response material.</summary>
internal enum NativeAccountOutcome
{
    NotReturned, Handled, Passthrough, Rejected
}
