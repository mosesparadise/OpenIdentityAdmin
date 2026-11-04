using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Antiforgery;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Validates one browser action before dispatch and suppresses output when its original session generation no longer matches.</summary>
internal static class ApplicationMutationProxy
{
    /// <summary>Provides the reviewed transport boundary for explicit create/edit/replacement routes; the API owns mutation authorization and commit.</summary>
    internal static Task HandleAsync(HttpContext http, ApplicationProxyAction action, string? id = null) =>
        ApplicationMutationBoundary.RunAsync(http, async (budget, clock) =>
        {
            var request = await ApplicationMutationRequest.ReadAsync(http, action, id, clock.GetUtcNow());
            var state = await ApplicationBrowserState.ReadAsync(http, clock) ?? throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
            http.Features.Set(state.Binding);
            await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
            budget.RequireCurrent();
            if (state.Expiry(clock.GetUtcNow()) is null)
            {
                throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
            }
            var result = await http.RequestServices.GetRequiredService<ApplicationProxyTransport>().WriteAsync(state.Access, request, budget.Token);
            budget.RequireCurrent();
            if (result.Details is null)
            {
                await ApplicationMutationBoundary.ProblemAsync(http, result.Status);
                return;
            }
            var current = await ApplicationBrowserState.ReadAsync(http, clock);
            budget.RequireCurrent();
            var now = clock.GetUtcNow();
            if (current is null || !state.Same(current) || state.Expiry(now) is not { } originalExpiry || current.Expiry(now) is not { } currentExpiry ||
                result.Details.ExpiresAt <= now || result.Credential is not null && result.Details.Credential!.ExpiresAt <= now)
            {
                throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
            }
            var expiry = new[] { originalExpiry, currentExpiry, result.Details.ExpiresAt }.Min();
            var details = result.Details with
            {
                ExpiresAt = expiry
            };
            object output = result.Credential is { } credential ? new ApplicationCredentialOutput(details, credential.ClientSecret) : details;
            http.Response.Headers.ETag = result.Version;
            await ApplicationMutationBoundary.WriteAsync(http, output, result.Status, budget, () => state.Expiry(clock.GetUtcNow()) is not null &&
                current.Expiry(clock.GetUtcNow()) is not null && details.ExpiresAt > clock.GetUtcNow() &&
                (result.Credential is null || details.Credential!.ExpiresAt > clock.GetUtcNow()));
        });
}
