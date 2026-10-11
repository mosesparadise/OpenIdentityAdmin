using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

// This HTTPS test client uses public metadata only. Tokens never enter a browser ticket or response.
public sealed class TestRelyingParty : IAsyncDisposable
{
    private static readonly HashSet<string> IdentityClaims = new(StringComparer.Ordinal)
    {
        "iss", "exp", "iat", "aud", "sub", "auth_time", "azp", "nonce", "at_hash", "oi_au_id", "oi_tkn_id"
    };
    private static readonly HashSet<string> AccessClaims = new(StringComparer.Ordinal)
    {
        "iss", "exp", "iat", "aud", "sub", "scope", "jti", "client_id", "oi_au_id", "oi_tkn_id", "oi_prst"
    };
    private readonly ConcurrentDictionary<string, TestFlow> flows = new(StringComparer.Ordinal);
    private readonly HttpClient backchannel = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(12) };
    private WebApplication? application;
    public required string ClientSecret
    {
        private get; init;
    }
    public Uri BaseUri { get; private set; } = null!;
    public Uri Issuer { get; set; } = null!;
    public Func<TestFlow, CancellationToken, Task<(string Subject, long AuthenticatedAt)>> ExpectedIdentity { get; set; } = null!;
    public string Callback => new Uri(BaseUri, "/callback").AbsoluteUri;

    public async Task StartAsync(CancellationToken cancellationToken, X509Certificate2? certificate = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0, listener => { if (certificate is null) { listener.UseHttps(); } else { listener.UseHttps(certificate); } }));
        application = builder.Build();
        application.MapGet("/alive", () => Results.Ok());
        application.MapGet("/start", (HttpContext context) =>
        {
            var id = context.Request.Query["flow"].ToString();
            if (!flows.TryGetValue(id, out var flow) || !flow.TryStart())
            {
                return Results.BadRequest();
            }
            context.Response.Cookies.Append("__Host-rp-" + id, flow.BrowserSecret, new CookieOptions
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = TimeSpan.FromMinutes(5)
            });
            return Results.Redirect(AuthorizationUri(flow));
        });
        application.MapGet("/callback", (Delegate)CompleteCallbackAsync);
        await application.StartAsync(cancellationToken);
        BaseUri = new Uri(application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
    }

    private async Task<IResult> CompleteCallbackAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        var state = context.Request.Query["state"].ToString();
        if (!flows.TryGetValue(state, out var flow) || !flow.Started || flow.ExpiresAt <= DateTimeOffset.UtcNow ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(context.Request.Cookies["__Host-rp-" + state] ?? ""), Encoding.UTF8.GetBytes(flow.BrowserSecret)) ||
            !flows.TryRemove(state, out _))
        {
            return Results.BadRequest("Invalid callback correlation.");
        }
        context.Response.Cookies.Delete("__Host-rp-" + state, new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Lax, Path = "/" });
        flow.RecordCallback();
        try
        {
            flow.Error = context.Request.Query["error"].ToString();
            flow.Code = context.Request.Query["code"].ToString();
            if (flow.Error.Length != 0 || flow.Code.Length == 0)
            {
                return Results.Content("Request denied", "text/plain");
            }
            if (!flow.Exchange)
            {
                flow.Completion.TrySetResult(true);
                return Results.Content("Authorization received", "text/plain");
            }
            flow.RecordExchange();
            using var response = await ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: context.RequestAborted);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                flow.ValidationFailure = "Token endpoint rejected the exchange.";
                return Results.Content("Exchange denied", "text/plain");
            }
            using var tokens = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.RequestAborted));
            flow.RememberTokens(tokens.RootElement);
            flow.ValidationFailure = await ValidateAsync(tokens.RootElement, flow, context.RequestAborted);
            flow.Validated = flow.ValidationFailure.Length == 0;
            flow.Completion.TrySetResult(flow.Validated);
            return Results.Content(flow.Validated ? "Signed in successfully" : "Validation denied", "text/plain");
        }
        catch (Exception)
        {
            flow.ValidationFailure = "The test relying party could not complete validation.";
            return Results.Content("Validation unavailable", "text/plain");
        }
        finally
        {
            flow.Completion.TrySetResult(false);
        }
    }

    public TestFlow Prepare(bool exchange = false, string? prompt = null, string? maximumAge = null, string? verifier = null)
    {
        var flow = new TestFlow(verifier) { Exchange = exchange, Prompt = prompt, MaximumAge = maximumAge };
        Assert.True(flows.TryAdd(flow.State, flow));
        return flow;
    }

    public Uri StartUri(TestFlow flow) => new(BaseUri, "/start?flow=" + flow.State);

    public string AuthorizationUri(TestFlow flow)
    {
        var values = new Dictionary<string, string?>
        {
            ["client_id"] = "identityplatform-pilot",
            ["redirect_uri"] = Callback,
            ["response_type"] = "code",
            ["scope"] = "openid pilot_api",
            ["state"] = flow.State,
            ["nonce"] = flow.Nonce,
            ["code_challenge_method"] = "S256",
            ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(flow.Verifier))),
            ["prompt"] = flow.Prompt,
            ["max_age"] = flow.MaximumAge
        };
        foreach (var value in flow.QueryOverrides)
        {
            values[value.Key] = value.Value;
        }
        return new Uri(Issuer, "/connect/authorize").AbsoluteUri + QueryString.Create(values.Where(value => value.Value is not null));
    }

    public async Task<HttpResponseMessage> ExchangeAsync(string code, string verifier, string? clientSecret = null,
        string? redirect = null, string clientId = "identityplatform-pilot", HttpClient? transport = null,
        string? cookie = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Issuer, "/connect/token"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + (clientSecret ?? ClientSecret))));
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = redirect ?? Callback
        });
        return await (transport ?? backchannel).SendAsync(request, cancellationToken);
    }

    public async Task<string> ValidateAsync(JsonElement tokens, TestFlow flow, CancellationToken cancellationToken, HttpClient? transport = null)
    {
        using var metadata = JsonDocument.Parse(await (transport ?? backchannel).GetStringAsync(new Uri(Issuer, "/.well-known/openid-configuration"), cancellationToken));
        if (metadata.RootElement.GetProperty("issuer").GetString() != Issuer.AbsoluteUri)
        {
            return "Metadata or token validation failed.";
        }
        var jwksUri = new Uri(metadata.RootElement.GetProperty("jwks_uri").GetString()!);
        if (jwksUri != new Uri(Issuer, "/connect/jwks"))
        {
            return "Metadata or token validation failed.";
        }
        var keys = new JsonWebKeySet(await (transport ?? backchannel).GetStringAsync(jwksUri, cancellationToken)).GetSigningKeys();
        var handler = new JsonWebTokenHandler();
        var id = await handler.ValidateTokenAsync(tokens.GetProperty("id_token").GetString()!, Parameters(Issuer.AbsoluteUri, "identityplatform-pilot", "JWT", keys));
        var access = await handler.ValidateTokenAsync(tokens.GetProperty("access_token").GetString()!, Parameters(Issuer.AbsoluteUri, "identityplatform-pilot-api", "at+jwt", keys));
        if (!id.IsValid || !access.IsValid || id.SecurityToken is not JsonWebToken identity || access.SecurityToken is not JsonWebToken api)
        {
            return "Independent JWT validation failed: " + (id.Exception?.GetType().Name ?? "ID valid") + ", " + (access.Exception?.GetType().Name ?? "access valid");
        }
        if (tokens.GetProperty("token_type").GetString() != "Bearer" || tokens.TryGetProperty("refresh_token", out _))
        {
            return "Token response profile mismatch.";
        }
        if (identity.ValidTo <= identity.IssuedAt || api.ValidTo <= api.IssuedAt ||
            identity.IssuedAt > DateTime.UtcNow || api.IssuedAt > DateTime.UtcNow ||
            !identity.TryGetClaim("azp", out var presenter) || presenter.Value != "identityplatform-pilot")
        {
            return "Token time or presenter mismatch.";
        }
        var expected = await ExpectedIdentity(flow, cancellationToken);
        if (!identity.TryGetClaim("nonce", out var nonceClaim) || nonceClaim.Value != flow.Nonce)
        {
            return "Nonce mismatch.";
        }
        if (identity.Subject != expected.Subject || api.Subject != expected.Subject)
        {
            return "Subject mismatch.";
        }
        if (!identity.Audiences.SequenceEqual(["identityplatform-pilot"]) || !api.Audiences.SequenceEqual(["identityplatform-pilot-api"]))
        {
            return "Audience mismatch.";
        }
        if (!api.TryGetClaim("client_id", out var client) || client.Value != "identityplatform-pilot")
        {
            return "Client identity mismatch.";
        }
        if (!api.TryGetClaim("scope", out var scope) || !scope.Value.Split(' ').ToHashSet(StringComparer.Ordinal).SetEquals(["openid", "pilot_api"]))
        {
            return "Scope mismatch.";
        }
        if (!identity.TryGetClaim("auth_time", out var authTime) || !long.TryParse(authTime.Value, out var authenticated) || authenticated != expected.AuthenticatedAt)
        {
            return "Authentication time mismatch.";
        }
        if (identity.ValidTo - identity.IssuedAt > TimeSpan.FromMinutes(5) || api.ValidTo - api.IssuedAt > TimeSpan.FromMinutes(5))
        {
            return "Lifetime mismatch.";
        }
        if (identity.Claims.Any(claim => !IdentityClaims.Contains(claim.Type)) || api.Claims.Any(claim => !AccessClaims.Contains(claim.Type)))
        {
            return "Unexpected claim names: " + string.Join(",", identity.Claims.Concat(api.Claims).Select(claim => claim.Type).Distinct());
        }
        if (!api.TryGetClaim("oi_prst", out var nativePresenter) || nativePresenter.Value != "identityplatform-pilot" ||
            !HasEntryReferences(identity) || !HasEntryReferences(api))
        {
            return "Native entry or presenter binding mismatch.";
        }
        return "";
    }

    private static bool HasEntryReferences(JsonWebToken token) =>
        token.TryGetClaim("oi_au_id", out var authorization) && authorization.Value.Length is > 0 and <= 100 &&
        token.TryGetClaim("oi_tkn_id", out var entry) && entry.Value.Length is > 0 and <= 100;

    internal static TokenValidationParameters Parameters(string issuer, string audience, string type, ICollection<SecurityKey> keys) => new()
    {
        ValidIssuer = issuer,
        ValidateIssuer = true,
        ValidAudience = audience,
        ValidateAudience = true,
        IssuerSigningKeys = keys,
        RequireSignedTokens = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.Zero,
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        ValidTypes = [type]
    };

    public async ValueTask DisposeAsync()
    {
        backchannel.Dispose();
        if (application is not null)
        {
            await application.DisposeAsync();
        }
    }
}

public sealed class TestFlow(string? verifier = null)
{
    private int started;
    private int callbacks;
    private int exchanges;
    private string[] tokenCanaries = [];
    public int CallbackCount => Volatile.Read(ref callbacks);
    public int ExchangeCount => Volatile.Read(ref exchanges);
    public void RecordCallback() => Interlocked.Increment(ref callbacks);
    public void RecordExchange() => Interlocked.Increment(ref exchanges);
    public void RememberTokens(JsonElement tokens) => tokenCanaries = [tokens.GetProperty("access_token").GetString()!, tokens.GetProperty("id_token").GetString()!];
    public bool ContainsNoTokens(string value) => tokenCanaries.Length == 2 && tokenCanaries.All(token => token.Length > 0 && !value.Contains(token, StringComparison.Ordinal));
    public bool TryStart() => ExpiresAt > DateTimeOffset.UtcNow && Interlocked.Exchange(ref started, 1) == 0;
    public bool Started => Volatile.Read(ref started) == 1;
    public DateTimeOffset ExpiresAt { get; } = DateTimeOffset.UtcNow.AddMinutes(5);
    public string State { get; } = NewSecret();
    public string Nonce { get; } = NewSecret();
    public string Verifier { get; } = verifier ?? NewSecret();
    public string BrowserSecret { get; } = NewSecret();
    public bool Exchange
    {
        get; init;
    }
    public string? Prompt
    {
        get; init;
    }
    public string? MaximumAge
    {
        get; init;
    }
    public Dictionary<string, string?> QueryOverrides { get; } = new(StringComparer.Ordinal);
    public string? InteractionId
    {
        get; set;
    }
    public string? ExpectedSessionId
    {
        get; set;
    }
    public long? ExpectedAuthenticatedAt
    {
        get; set;
    }
    public string Code { get; set; } = "";
    public string Error { get; set; } = "";
    public string ValidationFailure { get; set; } = "";
    public bool Validated
    {
        get; set;
    }
    public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static string NewSecret() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
}
