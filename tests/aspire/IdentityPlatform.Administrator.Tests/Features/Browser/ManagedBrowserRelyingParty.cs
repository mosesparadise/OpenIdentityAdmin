using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>A real trusted HTTPS relying party for the same UI-created client; credentials and tokens never enter its browser response.</summary>
internal sealed class ManagedBrowserRelyingParty : IAsyncDisposable
{
    private const string StartPath = "/start";
    private const string CallbackPath = "/callback";
    private const string CompletePath = "/complete";
    private const string AuthorizePath = "/connect/authorize";
    private const string TokenPath = "/connect/token";
    private const string CookieName = "__Host-managed-test-rp";
    private const string RootPath = "/";
    private const string HostName = "localhost";
    private const string NoStore = "no-store";
    private const string NoReferrer = "no-referrer";
    private const string ReferrerHeader = "Referrer-Policy";
    private const string ContentPolicy = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'none'";
    private const string ContentPolicyHeader = "Content-Security-Policy";
    private const string StateField = "state";
    private const string IssuerField = "iss";
    private const string CodeField = "code";
    private const string Invalid = "Managed callback rejected.";
    private const string Unavailable = "Managed validation unavailable.";
    private const string Basic = "Basic";
    private const string Separator = ":";
    private const string GrantField = "grant_type";
    private const string CodeGrant = "authorization_code";
    private const string RedirectField = "redirect_uri";
    private const string VerifierField = "code_verifier";
    private const string AccessField = "access_token";
    private const string IdentityField = "id_token";
    private const string TypeField = "token_type";
    private const string ExpiryField = "expires_in";
    private const string ScopeField = "scope";
    private const string Bearer = "Bearer";
    private const string OpenId = "openid";
    private const string JsonMediaType = "application/json";
    private const string HtmlMediaType = "text/html";
    private const string SetCookie = "Set-Cookie";
    private const string CompleteDocument = "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Application sign-in complete</title></head><body><main><h1>Application sign-in complete</h1></main></body></html>";
    private const int BackchannelSeconds = 12;
    private const int ShutdownSeconds = 10;
    private const int MaximumTokenBytes = 65536;
    private const int LifetimeSeconds = 300;
    private const int CallbackFields = 3;
    private static readonly string[] CallbackNames = [CodeField, IssuerField, StateField];
    private static readonly string[] TokenFields = [AccessField, ExpiryField, IdentityField, ScopeField, TypeField];
    private readonly HttpClient transport = new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
    {
        Timeout = TimeSpan.FromSeconds(BackchannelSeconds),
        MaxResponseContentBufferSize = MaximumTokenBytes
    };
    private WebApplication? application;
    private ManagedBrowserFlow? flow;
    internal Uri Issuer { get; private set; } = null!;
    internal Uri Origin { get; private set; } = null!;
    internal Uri Start => new(Origin, StartPath);
    internal Uri Complete => new(Origin, CompletePath);
    internal string Callback => new Uri(Origin, CallbackPath).AbsoluteUri;

    /// <summary>Starts a bound loopback TLS listener using ordinary development-certificate trust and disabled request logging.</summary>
    internal async Task StartAsync(Uri issuer)
    {
        Issuer = issuer;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Logging.ClearProviders();
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(ShutdownSeconds));
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listener => listener.UseHttps()));
        application = builder.Build();
        application.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = NoStore;
            context.Response.Headers[ReferrerHeader] = NoReferrer;
            context.Response.Headers[ContentPolicyHeader] = ContentPolicy;
            await next();
        });
        application.MapGet(StartPath, (Delegate)Begin);
        application.MapGet(CallbackPath, (Delegate)CallbackAsync);
        application.MapGet(CompletePath, () => Results.Content(CompleteDocument, HtmlMediaType));
        await application.StartAsync(TestContext.Current.CancellationToken);
        var bound = new Uri(application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        Origin = new UriBuilder(Uri.UriSchemeHttps, HostName, bound.Port).Uri;
    }

    /// <summary>Binds exactly the client and current secret returned by the actual editor; it never creates a replacement registration.</summary>
    internal ManagedBrowserFlow Prepare(ApplicationEditorRegistration registration, string subject)
    {
        if (flow is not null)
        {
            Assert.True(flow.Completion.Task.IsCompletedSuccessfully && flow.Completion.Task.Result);
            Assert.Equal(1, flow.Callbacks);
            Assert.Equal(1, flow.Exchanges);
        }
        Assert.True(registration.Callback == Callback);
        flow = new(registration, subject);
        flow.Initialize();
        return flow;
    }

    /// <summary>Establishes real secure browser correlation before redirecting to genuine native authorization caching.</summary>
    private IResult Begin(HttpContext context)
    {
        if (flow is null || !flow.TryStart())
        {
            return Results.BadRequest(Invalid);
        }
        context.Response.Cookies.Append(CookieName, flow.BrowserSecret, CookieOptions());
        return Results.Redirect(QueryHelpers.AddQueryString(new Uri(Issuer, AuthorizePath).AbsoluteUri,
            flow.Initial.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))));
    }

    /// <summary>Requires exact callback origin/path/query, original state, issuer and real HttpOnly browser correlation before exchange.</summary>
    private bool Correlated(HttpContext context, ManagedBrowserFlow current)
    {
        var query = context.Request.Query;
        return current.Started && current.ExpiresAt > DateTimeOffset.UtcNow && context.Request.IsHttps &&
            context.Request.Host.Value == Origin.Authority && context.Request.Path == CallbackPath &&
            query.Count == CallbackFields && query.Keys.Order(StringComparer.Ordinal).SequenceEqual(CallbackNames) &&
            query.All(pair => pair.Value.Count == 1) &&
            query[StateField] == current.State && query[IssuerField] == Issuer.AbsoluteUri && !string.IsNullOrEmpty(query[CodeField]) &&
            CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(context.Request.Cookies[CookieName] ?? string.Empty),
                Encoding.UTF8.GetBytes(current.BrowserSecret));
    }

    /// <summary>Consumes correlation once, exchanges only on the server and returns a static credential-free completion document.</summary>
    private async Task<IResult> CallbackAsync(HttpContext context)
    {
        var current = flow;
        if (current is null || !Correlated(context, current) || !current.TryCallback())
        {
            return Results.BadRequest(Invalid);
        }
        var cookie = CookieOptions();
        cookie.MaxAge = null;
        context.Response.Cookies.Delete(CookieName, cookie);
        try
        {
            using var response = await ExchangeAsync(current, context.Request.Query[CodeField].ToString(), context.RequestAborted);
            Assert.True(response.StatusCode == HttpStatusCode.OK && response.Headers.CacheControl?.NoStore == true &&
                response.Content.Headers.ContentType?.MediaType == JsonMediaType && !response.Headers.Contains(SetCookie));
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.RequestAborted));
            RequireResponse(body.RootElement);
            await ManagedIdentityTokenOracle.ValidateAsync(transport, Issuer, current.Registration.ClientId, current.Nonce, current.Subject, body.RootElement);
            current.Complete(body.RootElement);
            return Results.Redirect(CompletePath);
        }
        catch (Exception)
        {
            return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: Unavailable);
        }
        finally { current.Completion.TrySetResult(false); }
    }

    /// <summary>Uses the exact current one-time secret and independently retained S256 verifier without any browser token handoff.</summary>
    private Task<HttpResponseMessage> ExchangeAsync(ManagedBrowserFlow current, string code, CancellationToken token)
    {
        current.Exchange();
        return SendAsync(current, code, token);
    }

    /// <summary>Owns the form and Basic header until the bounded native backchannel has completed.</summary>
    private async Task<HttpResponseMessage> SendAsync(ManagedBrowserFlow current, string code, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Issuer, TokenPath))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [GrantField] = CodeGrant,
                [CodeField] = code,
                [VerifierField] = current.Verifier,
                [RedirectField] = Callback
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(Basic, Convert.ToBase64String(Encoding.UTF8.GetBytes(
            Uri.EscapeDataString(current.Registration.ClientId) + Separator + Uri.EscapeDataString(current.Registration.Secret))));
        return await transport.SendAsync(request, token);
    }

    /// <summary>Rejects every missing, duplicated or extra token-response member before cryptographic verification.</summary>
    private static void RequireResponse(JsonElement body)
    {
        Assert.True(body.EnumerateObject().Select(field => field.Name).Order(StringComparer.Ordinal).SequenceEqual(TokenFields));
        Assert.True(body.GetProperty(TypeField).GetString() == Bearer && body.GetProperty(ScopeField).GetString() == OpenId &&
            body.GetProperty(ExpiryField).GetInt64() is > 0 and <= LifetimeSeconds);
    }

    /// <summary>Uses host-only, Secure, HttpOnly and Lax test correlation bounded by the original flow lifetime.</summary>
    private static CookieOptions CookieOptions() => new()
    {
        Secure = true,
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Path = RootPath,
        MaxAge = TimeSpan.FromSeconds(LifetimeSeconds)
    };

    /// <summary>Joins the owned TLS host before releasing the server-side transport and private flow state.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (application is not null)
            {
                await application.DisposeAsync();
            }
        }
        finally { transport.Dispose(); flow = null; }
    }
}
