using IdentityPlatform.Admin.Bff.Features.Applications;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.Catalogue;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Admin.Bff.Features.Catalogue;

/// <summary>Issues a native catalogue-purpose proof bound to the currently observed durable browser generation.</summary>
internal static class CatalogueAntiforgeryEndpoint
{
    private const string TransferEncoding = "Transfer-Encoding";
    private const string ContentEncoding = "Content-Encoding";

    /// <summary>Requires exact bodyless same-origin acquisition without granting policy authority or renewing any session deadline.</summary>
    internal static Task HandleAsync(HttpContext http, string kind) => ApplicationMutationBoundary.RunAsync(http, async (budget, clock) =>
    {
        var path = CataloguePaths.BrowserRoot + CataloguePaths.Collection(kind) + CataloguePaths.Antiforgery;
        if (!ApplicationBrowserRequest.Origin(http, required: false) || !ApplicationBrowserRequest.Exact(http, path) ||
            !HttpMethods.IsGet(http.Request.Method) || http.Request.ContentLength is not (null or 0) ||
            http.Request.Headers.ContainsKey(TransferEncoding) || http.Request.Headers.ContainsKey(ContentEncoding) ||
            http.Request.Headers.ContainsKey(BrowserRequestPolicy.JsonAntiforgeryHeader) ||
            http.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody == true || !await BrowserLoginRequest.EmptyAsync(http))
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        var state = await ApplicationBrowserState.ReadAsync(http, clock, catalogue: true) ?? throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
        budget.RequireCurrent();
        if (state.Expiry(clock.GetUtcNow()) is null)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
        }
        http.Features.Set(state.Binding);
        var tokens = http.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);
        var output = new CatalogueAntiforgeryToken(tokens.RequestToken ?? throw new InvalidOperationException());
        await ApplicationMutationBoundary.WriteAsync(http, output, StatusCodes.Status200OK, budget, () => state.Expiry(clock.GetUtcNow()) is not null);
    }, CatalogueProxy.Title);
}

/// <summary>Contains only a native request token; the paired secure host-only cookie remains inaccessible to browser script.</summary>
internal sealed record CatalogueAntiforgeryToken(string RequestToken);
