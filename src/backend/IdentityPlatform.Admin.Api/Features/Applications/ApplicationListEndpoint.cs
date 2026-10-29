using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Http.Features;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Accepts one bounded native bearer request and releases a sanitized response only after original authority and cleanup checks.</summary>
internal static class ApplicationListEndpoint
{
    private const string NoStore = "no-store";
    private const string NoSniff = "nosniff";
    private const string TransferEncoding = "Transfer-Encoding";
    private const string InvalidQuery = "Invalid application query.";
    private const string SignInRequired = "Sign in required.";
    private const string Forbidden = "Application access is forbidden.";
    private const string Unavailable = "Applications are unavailable.";
    private const string CorrelationId = "correlationId";
    private const int QueryParts = 2;
    internal const string Path = "/api/v1/applications";
    private const string JsonType = "application/json; charset=utf-8";
    private const string BearerPrefix = "Bearer ";
    private const int MaximumAuthorizationLength = 16_391;

    /// <summary>Retains one deadline across native authentication, fresh authority, bounded query, serialization and disposal.</summary>
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

    /// <summary>Rejects request aliases/body/input before performing independent native access-token authentication.</summary>
    private static async Task ExecuteAsync(HttpContext http, ApplicationReadBudget budget, TimeProvider clock)
    {
        var request = http.Request;
        if (!ValidRequest(http) || request.QueryString.Value?.Length > ApplicationProjection.MaximumQueryCharacters ||
            ApplicationListQuery.Parse(request.Query.Select(pair => new KeyValuePair<string, string[]>(pair.Key, pair.Value.ToArray()!))) is not { } query)
        {
            await ProblemAsync(http, StatusCodes.Status400BadRequest);
            return;
        }
        if (request.Headers.Authorization is not { Count: 1 } header || header[0] is not { Length: > 0 and <= MaximumAuthorizationLength } value ||
            !value.StartsWith(BearerPrefix, StringComparison.Ordinal))
        {
            await ProblemAsync(http, StatusCodes.Status401Unauthorized);
            return;
        }
        var authentication = await ApiNativeAuthentication.AuthenticateAsync(http, budget, clock);
        if (authentication.Properties?.GetString(OpenIddictValidationAspNetCoreConstants.Properties.Error) == OpenIddictConstants.Errors.ServerError)
        {
            await ProblemAsync(http, StatusCodes.Status503ServiceUnavailable);
            return;
        }
        if (!authentication.Succeeded || authentication.Principal is not { } principal || ApiIdentity.Read(principal, clock.GetUtcNow()) is not { } identity)
        {
            await ProblemAsync(http, StatusCodes.Status401Unauthorized);
            return;
        }
        budget.RequireCurrent();
        await PublishAsync(http, budget, clock, identity, query);
    }

    /// <summary>Buffers a finite DTO until both owned scopes have disposed, then checks original token/source/deadline again.</summary>
    private static async Task PublishAsync(HttpContext http, ApplicationReadBudget budget, TimeProvider clock, ApiIdentity identity, ApplicationListQuery query)
    {
        var result = await ApplicationListOperation.ReadAsync(http.RequestServices.GetRequiredService<IServiceScopeFactory>(), identity, query,
            http.RequestServices.GetRequiredService<ApiResourceProfile>(), clock, budget.Token);
        budget.RequireCurrent();
        if (result.Status != StatusCodes.Status200OK || identity.ExpiresAt <= clock.GetUtcNow() || result.SourceExpiry <= clock.GetUtcNow().ToUnixTimeSeconds())
        {
            await ProblemAsync(http, result.Status == StatusCodes.Status200OK ? StatusCodes.Status401Unauthorized : result.Status);
            return;
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(result.Page, JsonSerializerOptions.Web);
        if (bytes.Length > ApplicationProjection.MaximumResponseBytes)
        {
            throw new InvalidOperationException();
        }
        budget.RequireCurrent();
        if (identity.ExpiresAt <= clock.GetUtcNow() || result.SourceExpiry <= clock.GetUtcNow().ToUnixTimeSeconds())
        {
            await ProblemAsync(http, StatusCodes.Status401Unauthorized);
            return;
        }
        http.Response.ContentType = JsonType;
        http.Response.ContentLength = bytes.Length;
        await http.Response.Body.WriteAsync(bytes, budget.Token);
    }

    /// <summary>Accepts only canonical HTTPS bodyless collection GETs, including the server's HTTP/QueryParts body-detection signal.</summary>
    private static bool ValidRequest(HttpContext http) => http.Request.IsHttps && HttpMethods.IsGet(http.Request.Method) &&
        http.Request.Path == Path && http.Request.ContentLength is null or 0 && !http.Request.Headers.ContainsKey(TransferEncoding) &&
        http.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody != true &&
        (http.Features.Get<IHttpRequestFeature>()?.RawTarget is not { Length: > 0 } raw || raw.Split('?', QueryParts)[0] == Path);

    /// <summary>Returns a finite safe problem without input values, native exception text or HTML authentication redirects.</summary>
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
