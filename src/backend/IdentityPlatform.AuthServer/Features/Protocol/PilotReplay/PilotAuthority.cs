using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Checks current persisted native and account authority after gate waits and after durable consumption.</summary>
internal static class PilotAuthority
{
    /// <summary>Uses the current phase's managers and exact code state without replay-validating this request's consumed continuation.</summary>
    internal static async ValueTask<bool> IsCurrentAsync(IServiceProvider services, PilotContinuation continuation, string status, CancellationToken token)
    {
        continuation.RequireCurrent();
        var validation = continuation.Validation(token);
        if (!PilotReplayCleanup.Matches(validation) || !await services.GetRequiredService<PilotReplayCleanup>().IsBoundAsync(validation, requireRevoked: false))
        {
            return false;
        }
        var tokens = services.GetRequiredService<IOpenIddictTokenManager>();
        var code = await tokens.FindByIdAsync(validation.TokenId!, token);
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        if (code is null || !await tokens.HasStatusAsync(code, status, token) ||
            !continuation.BindExpiration(await tokens.GetExpirationDateAsync(code, token)) || continuation.Expiration <= now ||
            continuation.Principal.GetExpirationDate() is not { } expiration || expiration <= now)
        {
            return false;
        }
        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var authorization = await authorizations.FindByIdAsync(validation.AuthorizationId!, token);
        if (authorization is null || !await authorizations.HasStatusAsync(authorization, OpenIddictConstants.Statuses.Valid, token))
        {
            return false;
        }
        var eligibility = services.GetRequiredService<PilotEligibility>();
        var principal = continuation.Principal;
        var eligible = await eligibility.SessionAsync(principal.GetClaim(PilotProfile.SessionClaim), principal.GetClaim(OpenIddictConstants.Claims.Subject), token) is not null &&
            await eligibility.RegistrationAsync(continuation.Context.ClientId, principal.GetScopes(), token) &&
            principal.GetResources().SequenceEqual([PilotProfile.Resource], StringComparer.Ordinal);
        continuation.RequireCurrent();
        PilotStorageFence.Current!.RequireCurrent();
        return eligible;
    }

    /// <summary>Discards only unchanged tracked snapshots so every authority read after a child or native generation reaches current storage.</summary>
    internal static void Refresh(IServiceProvider services)
    {
        var database = services.GetRequiredService<PlatformDbContext>();
        if (database.ChangeTracker.Entries().Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        database.ChangeTracker.Clear();
    }
}
