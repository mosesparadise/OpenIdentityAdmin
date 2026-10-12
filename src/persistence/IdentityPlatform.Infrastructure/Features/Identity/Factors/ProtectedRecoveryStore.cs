using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Factors;

/// <summary>Implements native recovery-store operations using irreversible verifiers and conditional claims.</summary>
internal sealed class ProtectedRecoveryStore(PlatformDbContext database, FactorAccess access, TimeProvider clock)
{
    /// <summary>Replaces exactly ten distinct verifiers, enlisted in the existing transaction.</summary>
    internal async Task ReplaceAsync(PlatformUser user, IEnumerable<string> codes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var generation = Generation(user);
        var values = codes.Take(RecoveryVerifier.Count + 1).ToArray();
        if (values.Length != RecoveryVerifier.Count || values.Distinct(StringComparer.Ordinal).Count() != RecoveryVerifier.Count ||
            values.Any(code => !RecoveryVerifier.IsCanonical(code)))
        {
            throw new FactorUnavailableException();
        }
        PrepareTrackedMaterial();
        await database.Set<AdministratorRecoveryCode>().Where(value => value.UserId == user.Id)
            .ExecuteDeleteAsync(cancellationToken);
        foreach (var code in values)
        {
            database.Add(new AdministratorRecoveryCode
            {
                UserId = user.Id,
                Generation = generation,
                Verifier = RecoveryVerifier.Hash(user.Id, generation, code)
            });
        }
    }

    /// <summary>Uses an atomic conditional update so a verifier cannot be redeemed twice across database connections.</summary>
    internal async Task<bool> RedeemAsync(PlatformUser user, string code, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var generation = Generation(user);
        if (!RecoveryVerifier.IsCanonical(code))
        {
            return false;
        }
        PrepareTrackedMaterial();
        var verifier = RecoveryVerifier.Hash(user.Id, generation, code);
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        return await database.Set<AdministratorRecoveryCode>().Where(value => value.UserId == user.Id &&
            value.Generation == generation && value.Verifier == verifier && value.RedeemedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(value => value.RedeemedAt, now), cancellationToken) == 1;
    }

    /// <summary>Counts the selected generation without exposing values or reviving spent codes.</summary>
    internal Task<int> CountAsync(PlatformUser user, CancellationToken cancellationToken)
    {
        var generation = Generation(user);
        return database.Set<AdministratorRecoveryCode>().CountAsync(value => value.UserId == user.Id &&
            value.Generation == generation && value.RedeemedAt == null, cancellationToken);
    }

    /// <summary>Rejects unsaved material and detaches clean snapshots before bulk writes to prevent stale reinsertion or claim reversal.</summary>
    private void PrepareTrackedMaterial()
    {
        var entries = database.ChangeTracker.Entries<AdministratorRecoveryCode>().ToArray();
        if (entries.Any(entry => entry.State != EntityState.Unchanged))
        {
            throw new FactorUnavailableException();
        }
        foreach (var entry in entries)
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>Requires scoped transaction authorization; it does not substitute for future ceremony locks/eligibility.</summary>
    private long Generation(PlatformUser user)
    {
        access.RequireCurrent();
        var enrollment = access.Recovery;
        if (enrollment is null || enrollment.Suspended || enrollment.RecoveryGeneration <= 0 ||
            !string.Equals(enrollment.UserId, user.Id, StringComparison.Ordinal))
        {
            throw new FactorUnavailableException();
        }
        return enrollment.RecoveryGeneration;
    }
}
