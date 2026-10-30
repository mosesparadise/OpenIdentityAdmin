using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Admin.Api.Features.Authentication;

/// <summary>Reads current account/session and persisted permission under rollback-only ownership; no factor keys or clock writes are used.</summary>
internal static class ApiCurrentAccess
{
    private const string ReadPermission = "applications.read";

    /// <summary>Distinguishes current authentication denial from an otherwise eligible account lacking its persisted grant.</summary>
    internal static async Task<(int Status, long SourceExpiry)> ReadAsync(IServiceProvider services, PlatformDbContext database,
        CeremonyTransaction transaction, ApiIdentity identity, string callback, TimeProvider clock)
    {
        transaction.RequireApiRead(database, identity.Subject);
        var state = await ApiSourceObservation.ReadAsync(database, identity, clock, transaction.Token);
        if (state is null || !await ApiReadProfile.AllowsAsync(services, database, transaction, identity, callback, clock.GetUtcNow().ToUnixTimeSeconds()))
        {
            return (StatusCodes.Status401Unauthorized, 0);
        }
        var grants = await new GovernanceGrants(database).ReadApiAsync(transaction, identity.Subject);
        transaction.RequireApiRead(database, identity.Subject);
        return (grants.Permissions(identity.Subject).Contains(ReadPermission) ? StatusCodes.Status200OK : StatusCodes.Status403Forbidden, state.ExpiresAt);
    }

}
