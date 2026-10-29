using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Projects one current browser session onto a fixed API read without exposing or accepting browser bearer credentials.</summary>
internal static class ApplicationProxyEndpoint
{
    private const string NoStore = "no-store";
    private const string NoSniff = "nosniff";
    private const string TransferEncoding = "Transfer-Encoding";
    private const string OriginHeader = "Origin";
    private const string FetchSiteHeader = "Sec-Fetch-Site";
    private const string SameOrigin = "same-origin";
    private const string NoSite = "none";
    private const string InvalidQuery = "Invalid application query.";
    private const string SignInRequired = "Sign in again to view applications.";
    private const string Forbidden = "Application access is forbidden.";
    private const string Unavailable = "Applications are unavailable.";
    private const string CorrelationId = "correlationId";
    private const int QueryParts = 2;
    internal const string Path = "/bff/api/v1/applications";
    private const string JsonType = "application/json; charset=utf-8";

    /// <summary>Owns the original browser deadline and clears failures without forwarding downstream headers or bodies.</summary>
    internal static async Task HandleAsync(HttpContext http)
    {
        http.Response.Headers.CacheControl = NoStore;
        http.Response.Headers.XContentTypeOptions = NoSniff;
        var clock = http.RequestServices.GetRequiredService<TimeProvider>();
        using var budget = new ApplicationReadBudget(clock, http.RequestAborted);
        var original = http.RequestAborted;
        http.RequestAborted = budget.Token;
        try
        {
            await ExecuteAsync(http, budget, clock);
        }
        catch (Exception) when (!http.Response.HasStarted)
        {
            http.RequestAborted = original;
            await ProblemAsync(http, StatusCodes.Status503ServiceUnavailable);
        }
        finally { http.RequestAborted = original; }
    }

    /// <summary>Requires bodyless same-origin input and a protected session credential before making any API request.</summary>
    private static async Task ExecuteAsync(HttpContext http, ApplicationReadBudget budget, TimeProvider clock)
    {
        if (!ValidRequest(http) || http.Request.QueryString.Value?.Length > ApplicationProjection.MaximumQueryCharacters ||
            ApplicationListQuery.Parse(http.Request.Query.Select(pair => new KeyValuePair<string, string[]>(pair.Key, pair.Value.ToArray()!))) is not { } query)
        {
            await ProblemAsync(http, StatusCodes.Status400BadRequest);
            return;
        }
        var (session, access) = await ReadCredentialAsync(http, clock);
        budget.RequireCurrent();
        if (session is null || access is null)
        {
            await ProblemAsync(http, StatusCodes.Status401Unauthorized);
            return;
        }
        var result = await http.RequestServices.GetRequiredService<ApplicationProxyTransport>().ReadAsync(access, query, budget.Token);
        budget.RequireCurrent();
        if (!session.Status(clock.GetUtcNow()).Authenticated || access.ExpiresAt <= clock.GetUtcNow())
        {
            await ProblemAsync(http, StatusCodes.Status401Unauthorized);
            return;
        }
        await PublishAsync(http, budget, result, session, access, clock);
    }

    /// <summary>Reads and disposes the original local session owner before any remote request; no activity or ticket state is changed.</summary>
    private static async Task<(BrowserSessionRead? Session, BrowserAccessCredential? Access)> ReadCredentialAsync(HttpContext http, TimeProvider clock)
    {
        await using var scope = http.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var session = await scope.ServiceProvider.GetRequiredService<BrowserSessionReader>().ReadAsync(http);
        var access = session?.Access(scope.ServiceProvider.GetRequiredService<SessionTicketCodec>(), clock.GetUtcNow());
        return (session, access);
    }

    /// <summary>Serializes only validated projected data and rechecks the original budget before response publication.</summary>
    private static async Task PublishAsync(HttpContext http, ApplicationReadBudget budget, (int Status, ApplicationPage? Page) result,
        BrowserSessionRead session, BrowserAccessCredential access, TimeProvider clock)
    {
        if (result.Status != StatusCodes.Status200OK || result.Page is null)
        {
            await ProblemAsync(http, result.Status);
            return;
        }
        var status = session.Status(clock.GetUtcNow());
        if (!status.Authenticated || status.ExpiresAt is not { } sessionExpiry || result.Page.ExpiresAt <= clock.GetUtcNow() ||
            result.Page.ExpiresAt.Offset != TimeSpan.Zero)
        {
            await ProblemAsync(http, StatusCodes.Status401Unauthorized);
            return;
        }
        var localExpiry = access.ExpiresAt < sessionExpiry ? access.ExpiresAt : sessionExpiry;
        var page = result.Page with
        {
            ExpiresAt = result.Page.ExpiresAt < localExpiry ? result.Page.ExpiresAt : localExpiry
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(page, JsonSerializerOptions.Web);
        if (bytes.Length > ApplicationProjection.MaximumResponseBytes)
        {
            throw new InvalidDataException();
        }
        budget.RequireCurrent();
        if (!session.Status(clock.GetUtcNow()).Authenticated || access.ExpiresAt <= clock.GetUtcNow() || page.ExpiresAt <= clock.GetUtcNow())
        {
            await ProblemAsync(http, StatusCodes.Status401Unauthorized);
            return;
        }
        http.Response.ContentType = JsonType;
        http.Response.ContentLength = bytes.Length;
        await http.Response.Body.WriteAsync(bytes, budget.Token);
    }

    /// <summary>Rejects encoded route aliases, body-bearing reads and explicit foreign browser provenance; no caller header is forwarded.</summary>
    private static bool ValidRequest(HttpContext http)
    {
        var origin = http.RequestServices.GetRequiredService<BrowserSessionDestinations>().Client.GetLeftPart(UriPartial.Authority);
        return http.Request.IsHttps && HttpMethods.IsGet(http.Request.Method) && http.Request.Path == Path &&
            http.Request.ContentLength is null or 0 && !http.Request.Headers.ContainsKey(TransferEncoding) &&
            http.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody != true &&
            (http.Features.Get<IHttpRequestFeature>()?.RawTarget is not { Length: > 0 } raw || raw.Split('?', QueryParts)[0] == Path) &&
            (!http.Request.Headers.TryGetValue(OriginHeader, out var incoming) || incoming.Count == 1 && incoming[0] == origin) &&
            (!http.Request.Headers.TryGetValue(FetchSiteHeader, out var site) || site.Count == 1 && site[0] is SameOrigin or NoSite);
    }

    /// <summary>Preserves authentication/permission distinctions using safe fixed problems rather than upstream content or redirects.</summary>
    private static Task ProblemAsync(HttpContext http, int status) => Results.Problem(statusCode: status,
        title: status switch
        {
            StatusCodes.Status400BadRequest => InvalidQuery,
            StatusCodes.Status401Unauthorized => SignInRequired,
            StatusCodes.Status403Forbidden => Forbidden,
            _ => Unavailable
        },
        extensions: new Dictionary<string, object?> { [CorrelationId] = http.TraceIdentifier }).ExecuteAsync(http);
}
