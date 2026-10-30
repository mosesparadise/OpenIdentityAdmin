using System.Text.Json;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Http.Features;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Owns bounded application HTTP admission, genuine native authentication and sanitized publication after child cleanup.</summary>
internal static class ApplicationHttpBoundary
{
    private const string NoStore = "no-store";
    private const string NoSniff = "nosniff";
    private const string JsonType = "application/json; charset=utf-8";
    private const string Bearer = "Bearer ";
    private const string VersionHeader = "ETag";
    private const string Correlation = "correlationId";
    private const string Invalid = "Invalid application request.";
    private const string SignIn = "Sign in required.";
    private const string Forbidden = "Application access is forbidden.";
    private const string Missing = "Application not found.";
    private const string Conflict = "Application conflicts with current registration state.";
    private const string Unavailable = "The application result is unavailable. Do not automatically resubmit.";
    private const int AuthorizationCharacters = 16391;

    /// <summary>Keeps one original budget across admission, authentication, transaction, disposal and response serialization.</summary>
    internal static async Task RunAsync(HttpContext http, Func<ApplicationReadBudget, TimeProvider, Task> operation)
    {
        http.Response.Headers.CacheControl = NoStore;
        http.Response.Headers.XContentTypeOptions = NoSniff;
        var clock = http.RequestServices.GetRequiredService<TimeProvider>();
        var original = http.RequestAborted;
        using var budget = new ApplicationReadBudget(clock, original);
        http.RequestAborted = budget.Token;
        try
        {
            await operation(budget, clock);
        }
        catch (Exception error) when (!http.Response.HasStarted)
        {
            http.RequestAborted = original;
            http.Response.Headers.Remove(VersionHeader);
            http.Response.ContentLength = null;
            await ProblemAsync(http, error is ApplicationHttpRejected rejected ? rejected.Status : StatusCodes.Status503ServiceUnavailable);
        }
        finally { http.RequestAborted = original; }
    }

    /// <summary>Accepts only a single bounded native bearer and disposes actual native validation before current action authority is observed.</summary>
    internal static async Task<ApiIdentity> AuthenticateAsync(HttpContext http, ApplicationReadBudget budget, TimeProvider clock)
    {
        if (http.Request.Headers.Authorization is not { Count: 1 } header || header[0] is not { Length: > 0 and <= AuthorizationCharacters } value ||
            !value.StartsWith(Bearer, StringComparison.Ordinal))
        {
            throw new ApplicationHttpRejected(StatusCodes.Status401Unauthorized);
        }
        var result = await ApiNativeAuthentication.AuthenticateAsync(http, budget, clock);
        if (result.Properties?.GetString(OpenIddictValidationAspNetCoreConstants.Properties.Error) == OpenIddictConstants.Errors.ServerError)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status503ServiceUnavailable);
        }
        if (!result.Succeeded || result.Principal is not { } principal || ApiIdentity.Read(principal, clock.GetUtcNow()) is not { } identity)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status401Unauthorized);
        }
        budget.RequireCurrent();
        return identity;
    }

    /// <summary>Rejects raw target aliases, query and non-HTTPS admission independently of routing normalization.</summary>
    internal static bool Exact(HttpContext http, string path) => http.Request.IsHttps && http.Request.Path.ToUriComponent() == path &&
        !http.Request.QueryString.HasValue && (http.Features.Get<IHttpRequestFeature>()?.RawTarget is not { Length: > 0 } raw || raw == path);

    /// <summary>Serializes only finite projections after disposal, then repeats original authority and budget checks before publishing any body.</summary>
    internal static async Task PublishAsync(HttpContext http, object output, int status, string? version, ApplicationReadBudget budget, Func<bool> current)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(output, output.GetType(), ApplicationJson.Options);
        if (bytes.Length > ApplicationProjection.MaximumResponseBytes)
        {
            throw new InvalidDataException();
        }
        budget.RequireCurrent();
        if (!current())
        {
            throw new ApplicationHttpRejected(StatusCodes.Status401Unauthorized);
        }
        if (version is not null)
        {
            http.Response.Headers.ETag = version;
        }
        http.Response.StatusCode = status;
        http.Response.ContentType = JsonType;
        http.Response.ContentLength = bytes.Length;
        await http.Response.Body.WriteAsync(bytes, budget.Token);
    }

    /// <summary>Maps finite statuses to fixed safe messages without native errors, submitted fields or credentials.</summary>
    private static Task ProblemAsync(HttpContext http, int status) => Results.Problem(statusCode: status, title: status switch
    {
        StatusCodes.Status400BadRequest => Invalid,
        StatusCodes.Status401Unauthorized => SignIn,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => Missing,
        StatusCodes.Status409Conflict => Conflict,
        _ => Unavailable
    }, extensions: new Dictionary<string, object?> { [Correlation] = http.TraceIdentifier }).ExecuteAsync(http);
}

/// <summary>Carries only a finite public status across the local HTTP boundary.</summary>
internal sealed class ApplicationHttpRejected(int status) : Exception
{
    internal int Status => status;
}
