using Microsoft.AspNetCore.Antiforgery;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Issues only a native application-purpose token after authenticating the actual durable browser generation.</summary>
internal static class ApplicationAntiforgeryEndpoint
{
    internal const string Path = "/bff/api/v1/applications/antiforgery";

    /// <summary>Acquires no mutation authority or OAuth material for the browser and does not renew the session.</summary>
    internal static Task HandleAsync(HttpContext http) => ApplicationMutationBoundary.RunAsync(http, async (budget, clock) =>
    {
        if (!await ApplicationBrowserRequest.TokenAsync(http))
        {
            throw new ApplicationProxyRejected(StatusCodes.Status400BadRequest);
        }
        var state = await ApplicationBrowserState.ReadAsync(http, clock) ?? throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
        budget.RequireCurrent();
        if (state.Expiry(clock.GetUtcNow()) is null)
        {
            throw new ApplicationProxyRejected(StatusCodes.Status401Unauthorized);
        }
        http.Features.Set(state.Binding);
        var tokens = http.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(http);
        var result = new ApplicationAntiforgeryToken(tokens.RequestToken ?? throw new InvalidOperationException());
        await ApplicationMutationBoundary.WriteAsync(http, result, StatusCodes.Status200OK, budget, () => state.Expiry(clock.GetUtcNow()) is not null);
    });
}

/// <summary>Contains only the native request token; the paired cookie is secure, host-only and inaccessible to script.</summary>
internal sealed record ApplicationAntiforgeryToken(string RequestToken);
