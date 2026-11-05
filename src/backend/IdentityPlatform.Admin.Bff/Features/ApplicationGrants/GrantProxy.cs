using IdentityPlatform.Admin.Bff.Features.Applications;
using IdentityPlatform.Core.Features.ApplicationGrants;
using Microsoft.AspNetCore.Antiforgery;

namespace IdentityPlatform.Admin.Bff.Features.ApplicationGrants;

/// <summary>Retains the original browser generation and expiry around one independently authorized API job operation.</summary>
internal static class GrantProxy
{
    private const string Separator = "/";

    /// <summary>Validates native CSRF for writes and rechecks the same durable browser credentials before any safe response is published.</summary>
    internal static Task HandleAsync(HttpContext http, string id, string? jobId, bool mutation) => ApplicationMutationBoundary.RunAsync(http, async (budget, clock) =>
    {
        var input = await GrantProxyRequest.ReadAsync(http, id, jobId, mutation);
        var original = await ApplicationBrowserState.ReadAsync(http, clock) ?? throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
        http.Features.Set(original.Binding);
        if (mutation)
        {
            await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
        }
        budget.RequireCurrent();
        if (original.Expiry(clock.GetUtcNow()) is null)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
        }
        var result = await http.RequestServices.GetRequiredService<ApplicationProxyTransport>().GrantsAsync(original.Access, input, budget.Token);
        if (result.Job is null && result.Page is null)
        {
            throw new ApplicationProxyRejected(result.Status);
        }
        var current = await ApplicationBrowserState.ReadAsync(http, clock);
        budget.RequireCurrent();
        var now = clock.GetUtcNow();
        var apiExpiry = result.Job?.ExpiresAt ?? result.Page!.ExpiresAt;
        if (current is null || !original.Same(current) || original.Expiry(now) is not { } first || current.Expiry(now) is not { } last || apiExpiry <= now)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
        }
        var expiry = new[] { first, last, apiExpiry }.Min();
        var output = Project(http, input, result, expiry);
        await ApplicationMutationBoundary.WriteAsync(http, output, result.Status, budget, () =>
            original.Expiry(clock.GetUtcNow()) is not null && current.Expiry(clock.GetUtcNow()) is not null && expiry > clock.GetUtcNow());
    });

    /// <summary>Clamps every nested observation to original browser authority and emits only code-owned status locations.</summary>
    private static object Project(HttpContext http, GrantProxyRequest input, GrantProxyResult result, DateTimeOffset expiry)
    {
        if (result.Page is { } page)
        {
            return new GrantJobPage(page.Items.Select(job => job with { ExpiresAt = expiry }).ToArray(), expiry);
        }
        var job = result.Job! with
        {
            ExpiresAt = expiry
        };
        http.Response.Headers.ETag = job.Version;
        if (!input.Mutation)
        {
            return job;
        }
        http.Response.Headers.Location = ApplicationProxyEndpoint.Path + Separator + Uri.EscapeDataString(input.Id) + GrantRevocationRules.RouteSuffix + Separator + job.Id;
        return new GrantAcceptance(job, input.Key!);
    }
}
