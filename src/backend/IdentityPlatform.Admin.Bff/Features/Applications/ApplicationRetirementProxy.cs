using IdentityPlatform.Core.Features.ApplicationGrants;
using Microsoft.AspNetCore.Antiforgery;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Retains the original browser generation and independent native CSRF validation around one explicit terminal admission.</summary>
internal static class ApplicationRetirementProxy
{
    private const string Separator = "/";

    /// <summary>Rechecks the same protected browser state after API disposal and clamps safe job observation to the original useful lifetime.</summary>
    internal static Task HandleAsync(HttpContext http, string id) => ApplicationMutationBoundary.RunAsync(http, async (budget, clock) =>
    {
        var input = await ApplicationMutationRequest.ReadAsync(http, ApplicationProxyAction.Retire, id, clock.GetUtcNow());
        var original = await ApplicationBrowserState.ReadAsync(http, clock) ?? throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
        http.Features.Set(original.Binding);
        await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
        budget.RequireCurrent();
        if (original.Expiry(clock.GetUtcNow()) is null)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
        }
        var result = await http.RequestServices.GetRequiredService<ApplicationProxyTransport>().RetireAsync(original.Access, input, budget.Token);
        var accepted = result.Acceptance ?? throw new ApplicationProxyRejected(result.Status);
        var current = await ApplicationBrowserState.ReadAsync(http, clock);
        budget.RequireCurrent();
        var now = clock.GetUtcNow();
        if (current is null || !original.Same(current) || original.Expiry(now) is not { } first || current.Expiry(now) is not { } last ||
            accepted.Job.ExpiresAt <= now || accepted.RetiredAt > now)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
        }
        var expiry = new[] { first, last, accepted.Job.ExpiresAt }.Min();
        var output = accepted with
        {
            Job = accepted.Job with
            {
                ExpiresAt = expiry
            }
        };
        http.Response.Headers.ETag = output.Job.Version;
        http.Response.Headers.Location = ApplicationProxyEndpoint.Path + Separator + Uri.EscapeDataString(id) + GrantRevocationRules.RouteSuffix + Separator + output.Job.Id;
        await ApplicationMutationBoundary.WriteAsync(http, output, StatusCodes.Status202Accepted, budget, () =>
            original.Expiry(clock.GetUtcNow()) is not null && current.Expiry(clock.GetUtcNow()) is not null && expiry > clock.GetUtcNow());
    });
}
