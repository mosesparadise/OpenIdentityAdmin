using IdentityPlatform.Admin.Bff.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using Microsoft.AspNetCore.Antiforgery;

namespace IdentityPlatform.Admin.Bff.Features.Catalogue;

/// <summary>Joins finite catalogue transport with current durable cookie binding, native CSRF and cleanup-before-publication.</summary>
internal static class CatalogueProxy
{
    private const string Invalid = "Invalid catalogue request.";
    private const string SignIn = "Sign in again to manage the catalogue.";
    private const string Forbidden = "Catalogue access is forbidden.";
    private const string Missing = "Catalogue entry or deletion job not found.";
    private const string Conflict = "Reload the catalogue entry and review its current references before submitting.";
    private const string Unavailable = "The catalogue result is unavailable. Check its status before submitting again.";

    /// <summary>Sends one request only after local validation, then rechecks the original durable generation and every observed expiry.</summary>
    internal static Task HandleAsync(HttpContext http, string kind, CatalogueProxyAction action, string? id = null, string? jobId = null) =>
        ApplicationMutationBoundary.RunAsync(http, async (budget, clock) =>
        {
            var request = await CatalogueProxyRequest.ReadAsync(http, kind, action, id, jobId);
            var state = await ApplicationBrowserState.ReadAsync(http, clock, catalogue: true) ?? throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
            if (request.Mutation)
            {
                http.Features.Set(state.Binding);
                await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
            }
            budget.RequireCurrent();
            if (state.Expiry(clock.GetUtcNow()) is null)
            {
                throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
            }
            var result = await http.RequestServices.GetRequiredService<ApplicationProxyTransport>().CatalogueAsync(state.Access, request, budget.Token);
            if (result.Output is null)
            {
                throw new ApplicationProxyRejected(result.Status);
            }
            var current = await ApplicationBrowserState.ReadAsync(http, clock, catalogue: true);
            var now = clock.GetUtcNow();
            if (current is null || !state.Same(current) || state.Expiry(now) is not { } original || current.Expiry(now) is not { } latest ||
                result.ExpiresAt is not { } remote || remote <= now)
            {
                throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
            }
            var expiry = new[] { original, latest, remote }.Min();
            http.Response.Headers.ETag = result.Version;
            if (action == CatalogueProxyAction.Delete && result.Output is CatalogueDeletionDetails job)
            {
                http.Response.Headers.Location = CataloguePaths.BrowserRoot + CataloguePaths.Job(kind, job.Id);
            }
            await ApplicationMutationBoundary.WriteAsync(http, result.Narrow(expiry), result.Status, budget, () =>
                state.Expiry(clock.GetUtcNow()) is not null && current.Expiry(clock.GetUtcNow()) is not null && expiry > clock.GetUtcNow());
        }, Title);

    /// <summary>Maps only finite statuses to value-free browser recovery text; upstream bodies never become error responses.</summary>
    internal static string Title(int status) => status switch
    {
        StatusCodes.Status400BadRequest => Invalid,
        StatusCodes.Status401Unauthorized => SignIn,
        StatusCodes.Status403Forbidden => Forbidden,
        StatusCodes.Status404NotFound => Missing,
        StatusCodes.Status409Conflict or StatusCodes.Status412PreconditionFailed or StatusCodes.Status428PreconditionRequired => Conflict,
        _ => Unavailable
    };
}
