using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Observes current batch authority without retaining browser credentials or requiring fresh session/MFA proof.</summary>
internal static class GrantActor
{
    private const int FactorMultiplicity = 2;

    /// <summary>Requires the effective approver's ordered locks and actual persisted grant; malformed storage propagates as unavailable.</summary>
    internal static ValueTask<bool> AllowsAsync(ProtocolStorageScope scope, ProtocolAdmissionOwnership ownership, CancellationToken token)
    {
        var database = (PlatformDbContext)scope.Owner.Database;
        return ownership.RunAsync(database, async current =>
        {
            if (!await AccountAsync(database, ownership.UserId, scope.Owner.Clock, current))
            {
                return false;
            }
            var grants = await new GovernanceGrants(database).ReadProtocolAsync(ownership, current);
            return grants.Permissions(ownership.UserId).Contains(GrantRevocationRules.Permission);
        }, token);
    }

    /// <summary>Checks active account, enrollment, lockout budget and factor metadata without loading or decrypting protected seed material.</summary>
    private static async Task<bool> AccountAsync(PlatformDbContext database, string actor, TimeProvider clock, CancellationToken token)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(row => row.Id == actor, token);
        var enrollment = await database.Set<AdministratorEnrollment>().AsNoTracking().SingleOrDefaultAsync(row => row.UserId == actor, token);
        var authentication = await database.Set<AdministratorAuthentication>().AsNoTracking().SingleOrDefaultAsync(row => row.UserId == actor, token);
        if (user is null || enrollment is null || authentication is null || user.Id != actor || enrollment.UserId != actor || authentication.UserId != actor ||
            !user.IsEnabled || !user.TwoFactorEnabled || !user.LockoutEnabled || user.AccessFailedCount < 0 ||
            string.IsNullOrEmpty(user.SecurityStamp) || string.IsNullOrEmpty(user.PasswordHash) ||
            enrollment.State != AdministratorPolicy.Active || enrollment.Suspended || enrollment.FactorGeneration <= 0 ||
            enrollment.Revision < 0 || enrollment.RecoveryGeneration < 0 || authentication.Revision < 0 ||
            authentication.LastAllocatedFactorGeneration < enrollment.FactorGeneration ||
            !new CeremonyClock(clock).TryRead(authentication, out var now) ||
            !CeremonyBudget.IsAvailable(user, authentication, Math.Max(now, authentication.ObservedAt ?? now)))
        {
            return false;
        }
        var factors = await database.Set<AdministratorFactor>().AsNoTracking()
            .Where(row => row.UserId == actor && row.Generation == enrollment.FactorGeneration)
            .Select(row => new { row.UserId, row.Generation, Present = row.ProtectedSeed != string.Empty })
            .Take(FactorMultiplicity).ToArrayAsync(token);
        return factors is [var factor] && factor.UserId == actor && factor.Generation == enrollment.FactorGeneration && factor.Present;
    }
}
