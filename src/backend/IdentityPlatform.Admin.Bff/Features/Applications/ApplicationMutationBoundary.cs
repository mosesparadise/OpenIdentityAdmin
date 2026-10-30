using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Antiforgery;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Owns the original browser budget and safe output for token acquisition and the finite mutation transport.</summary>
internal static class ApplicationMutationBoundary
{
    private const string VersionHeader = "ETag";
    private const string CookieHeader = "Set-Cookie";

    private const string NoStore = "no-store";
    private const string NoSniff = "nosniff";
    private const string JsonType = "application/json; charset=utf-8";
    private const string Invalid = "Invalid application request.";
    private const string SignIn = "Sign in again to manage applications.";
    private const string Forbidden = "Application access is forbidden.";
    private const string Conflict = "Reload the application before submitting a new change.";
    private const string Unavailable = "The application result is unavailable. Do not automatically resubmit.";
    private const string Correlation = "correlationId";

    /// <summary>Starts one budget before reading sessions or input and sanitizes failures without reflecting upstream values.</summary>
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
            http.Response.Headers.Remove(CookieHeader);
            http.Response.ContentLength = null;
            var status = error is ApplicationProxyRejected rejected ? rejected.Status : error is AntiforgeryValidationException ?
                StatusCodes.Status400BadRequest : StatusCodes.Status503ServiceUnavailable;
            await ProblemAsync(http, status);
        }
        finally
        {
            http.RequestAborted = original;
            http.Features.Set<BrowserRequestBinding>(null);
        }
    }

    /// <summary>Serializes a fully validated DTO before the last budget check; no upstream headers or partial buffered secret are forwarded.</summary>
    internal static async Task WriteAsync(HttpContext http, object value, int status, ApplicationReadBudget budget, Func<bool> current)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), ApplicationJson.Options);
        if (bytes.Length > ApplicationProjection.MaximumResponseBytes)
        {
            throw new InvalidDataException();
        }
        budget.RequireCurrent();
        if (!current())
        {
            throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
        }
        http.Response.StatusCode = status;
        http.Response.ContentType = JsonType;
        http.Response.ContentLength = bytes.Length;
        await http.Response.Body.WriteAsync(bytes, budget.Token);
    }

    /// <summary>Reports only finite statuses and fixed recovery text; an unavailable response never promises remote rollback.</summary>
    internal static Task ProblemAsync(HttpContext http, int status) => Results.Problem(statusCode: status,
        title: status switch
        {
            StatusCodes.Status400BadRequest => Invalid,
            StatusCodes.Status401Unauthorized => SignIn,
            StatusCodes.Status403Forbidden => Forbidden,
            StatusCodes.Status404NotFound or StatusCodes.Status409Conflict or StatusCodes.Status412PreconditionFailed or StatusCodes.Status428PreconditionRequired => Conflict,
            _ => Unavailable
        }, extensions: new Dictionary<string, object?> { [Correlation] = http.TraceIdentifier }).ExecuteAsync(http);
}
