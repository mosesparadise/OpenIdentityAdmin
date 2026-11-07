using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Observes current batch authority without retaining browser credentials or requiring fresh session/MFA proof.</summary>
internal static class GrantActor
{
    /// <summary>Requires the effective approver's ordered locks and actual persisted grant; malformed storage propagates as unavailable.</summary>
    internal static ValueTask<bool> AllowsAsync(ProtocolStorageScope scope, ProtocolAdmissionOwnership ownership, CancellationToken token)
    {
        var database = (PlatformDbContext)scope.Owner.Database;
        return ownership.RunAsync(database, async current =>
        {
            if (await AdministratorCurrentAccount.ReadAsync(database, ownership.UserId, scope.Owner.Clock, current) is null)
            {
                return false;
            }
            var grants = await new GovernanceGrants(database).ReadProtocolAsync(ownership, current);
            return grants.Permissions(ownership.UserId).Contains(GrantRevocationRules.Permission);
        }, token);
    }

}
