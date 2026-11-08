using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Shares current administrator account/factor eligibility without renewing any caller's authority or exposing protected factor material.</summary>
internal static class AdministratorCurrentAccount
{
    private const int FactorMultiplicity = 2;

    /// <summary>Checks active account, enrollment, lockout budget and factor metadata without loading or decrypting protected seed material.</summary>
    internal static async Task<AdministratorCurrentBinding?> ReadAsync(PlatformDbContext database, string actor, TimeProvider clock, CancellationToken token)
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
            return null;
        }
        var factors = await database.Set<AdministratorFactor>().AsNoTracking()
            .Where(row => row.UserId == actor && row.Generation == enrollment.FactorGeneration)
            .Select(row => new { row.UserId, row.Generation, Present = row.ProtectedSeed != string.Empty })
            .Take(FactorMultiplicity).ToArrayAsync(token);
        return factors is [var factor] && factor.UserId == actor && factor.Generation == enrollment.FactorGeneration && factor.Present
            ? new(user.SecurityStamp, authentication.Generation, enrollment.FactorGeneration, enrollment.Revision) : null;
    }
}

/// <summary>Freezes observed security bindings; callers still require their own action permission, locks and original approval bounds.</summary>
internal sealed record AdministratorCurrentBinding(string Stamp, long Generation, long FactorGeneration, long EnrollmentRevision);
