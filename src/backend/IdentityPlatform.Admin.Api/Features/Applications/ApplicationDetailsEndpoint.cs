using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Admin.Api.Features.Applications;

/// <summary>Publishes sanitized current registration details and an editable aggregate version without returning native credentials.</summary>
internal static class ApplicationDetailsEndpoint
{
    private const string Separator = "/";
    private const string Current = ".";
    private const string Parent = "..";
    private const string TransferEncoding = "Transfer-Encoding";
    private const string ContentEncoding = "Content-Encoding";

    /// <summary>Requires exact bodyless HTTPS admission and genuine bearer validation before a separately owned read-only operation.</summary>
    internal static Task HandleAsync(HttpContext http, string id) => ApplicationHttpBoundary.RunAsync(http, async (budget, clock) =>
    {
        if (!ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength) || id is Current or Parent ||
            !ApplicationHttpBoundary.Exact(http, ApplicationListEndpoint.Path + Separator + Uri.EscapeDataString(id)) || !HttpMethods.IsGet(http.Request.Method) ||
            http.Request.ContentLength is not (null or 0) || http.Request.Headers.ContainsKey(TransferEncoding) || http.Request.Headers.ContainsKey(ContentEncoding) ||
            http.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true || await http.Request.Body.ReadAsync(new byte[1], http.RequestAborted) != 0)
        {
            throw new ApplicationHttpRejected(StatusCodes.Status400BadRequest);
        }
        var identity = await ApplicationHttpBoundary.AuthenticateAsync(http, budget, clock);
        var result = await ApplicationDetailsOperation.ReadAsync(http.RequestServices.GetRequiredService<IServiceScopeFactory>(), identity, id,
            http.RequestServices.GetRequiredService<ApiResourceProfile>(), clock, budget.Token);
        await ApplicationHttpBoundary.PublishAsync(http, result.Details, StatusCodes.Status200OK, result.Version, budget,
            () => identity.ExpiresAt > clock.GetUtcNow() && result.Details.ExpiresAt > clock.GetUtcNow());
    });
}
