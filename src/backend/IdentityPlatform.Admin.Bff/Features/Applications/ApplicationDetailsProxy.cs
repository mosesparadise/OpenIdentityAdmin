using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Reads exact safe details using only current protected server credentials and rechecks durable generation before publication.</summary>
internal static class ApplicationDetailsProxy
{
    private const string Separator = "/";
    private const string Current = ".";
    private const string Parent = "..";
    private const string TokenResource = "antiforgery";
    private const string TransferEncoding = "Transfer-Encoding";
    private const string ContentEncoding = "Content-Encoding";

    /// <summary>Retains the original budget and token lifetime without renewing the browser session or exposing upstream credential state.</summary>
    internal static Task HandleAsync(HttpContext http, string id) => ApplicationMutationBoundary.RunAsync(http, async (budget, clock) =>
    {
        if (!await ValidAsync(http, id))
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        var state = await ApplicationBrowserState.ReadAsync(http, clock) ?? throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
        budget.RequireCurrent();
        var result = await http.RequestServices.GetRequiredService<ApplicationProxyTransport>().DetailsAsync(state.Access, id, budget.Token);
        if (result.Details is not { } details)
        {
            throw new ApplicationProxyRejected(result.Status);
        }
        var current = await ApplicationBrowserState.ReadAsync(http, clock);
        var now = clock.GetUtcNow();
        if (current is null || !state.Same(current) || state.Expiry(now) is not { } original || current.Expiry(now) is not { } latest || details.ExpiresAt <= now)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
        }
        var output = details with
        {
            ExpiresAt = new[] { original, latest, details.ExpiresAt }.Min()
        };
        if (result.Version is not null)
        {
            http.Response.Headers.ETag = result.Version;
        }
        await ApplicationMutationBoundary.WriteAsync(http, output, StatusCodes.Status200OK, budget, () =>
            state.Expiry(clock.GetUtcNow()) is not null && current.Expiry(clock.GetUtcNow()) is not null && output.ExpiresAt > clock.GetUtcNow());
    });

    /// <summary>Reserves the existing literal token route and rejects aliases, bodies and browser bearer credentials.</summary>
    private static async Task<bool> ValidAsync(HttpContext http, string id) => ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength) &&
        id is not (Current or Parent) && !string.Equals(id, TokenResource, StringComparison.OrdinalIgnoreCase) && ApplicationBrowserRequest.Origin(http, required: false) &&
        ApplicationBrowserRequest.Exact(http, ApplicationProxyEndpoint.Path + Separator + Uri.EscapeDataString(id)) && HttpMethods.IsGet(http.Request.Method) &&
        http.Request.ContentLength is null or 0 && !http.Request.Headers.ContainsKey(TransferEncoding) && !http.Request.Headers.ContainsKey(ContentEncoding) &&
        http.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody != true && await BrowserLoginRequest.EmptyAsync(http);
}
