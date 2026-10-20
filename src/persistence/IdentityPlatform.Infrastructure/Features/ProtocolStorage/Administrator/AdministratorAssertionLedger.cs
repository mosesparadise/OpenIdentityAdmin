using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Reserves bounded shared PAR/token replay state in the original explicit owning transaction without retry.</summary>
internal static class AdministratorAssertionLedger
{
    internal const int RetainedLimit = 10000;
    internal const int CleanupBatch = 100;

    /// <summary>Acquires profile serialization before observing time, reclaiming eligible tombstones or inserting one unique digest.</summary>
    internal static async ValueTask<bool> ReserveAsync(ProtocolStorageOwner owner, AdministratorAssertion assertion, CancellationToken token)
    {
        await AdministratorProtocolLocks.AcquireProfileAsync(owner, token);
        return await ReserveLockedAsync(owner, assertion, token) is not null;
    }

    /// <summary>Reserves after the caller has acquired profile serialization and verified the complete current registration in that same transaction.</summary>
    internal static ValueTask<long?> ReserveLockedAsync(ProtocolStorageOwner owner, AdministratorAssertion assertion, CancellationToken token)
    {
        return owner.RunAsync<long?>(async cancellation =>
        {
            var profile = await owner.Database.Set<AdministratorProtocolProfile>().SingleOrDefaultAsync(
                row => row.Id == AdministratorProtocolProfile.SingletonId, cancellation);
            if (profile is not { Enabled: true } || !TryTime(profile, owner.Clock, out var now))
            {
                return null;
            }
            if (!assertion.Allows(now))
            {
                return null;
            }
            await CleanupAsync(owner.Database, now, cancellation);
            var ledger = owner.Database.Set<AdministratorClientAssertionUse>();
            if (await ledger.AnyAsync(row => row.Digest == assertion.Digest, cancellation) || await ledger.CountAsync(cancellation) >= RetainedLimit)
            {
                return null;
            }
            profile.ObservedAt = now;
            ledger.Add(new AdministratorClientAssertionUse
            {
                Digest = assertion.Digest,
                CreatedAt = now,
                ExpiresAt = assertion.ExpiresAt,
                RetainUntil = assertion.ExpiresAt + AdministratorAssertion.ClockSkewSeconds
            });
            await owner.Database.SaveChangesAsync(cancellation);
            return TryTime(profile, owner.Clock, out var finalNow) && assertion.Allows(finalNow) ? profile.ObservedAt : null;
        }, token, write: true);
    }

    /// <summary>Deletes at most one deterministic expired batch and rechecks retention in the actual delete predicate.</summary>
    private static async Task CleanupAsync(DbContext database, long now, CancellationToken token)
    {
        var ledger = database.Set<AdministratorClientAssertionUse>();
        var expired = await ledger.AsNoTracking().Where(row => row.RetainUntil < now).OrderBy(row => row.RetainUntil).ThenBy(row => row.Digest)
            .Select(row => row.Digest).Take(CleanupBatch).ToArrayAsync(token);
        if (expired.Length != 0)
        {
            await ledger.Where(row => expired.Contains(row.Digest) && row.RetainUntil < now).ExecuteDeleteAsync(token);
        }
    }

    /// <summary>Rejects a rollback beyond tolerance at each observation while never extending validity through a smaller accepted sample.</summary>
    private static bool TryTime(AdministratorProtocolProfile profile, TimeProvider clock, out long now)
    {
        var sample = clock.GetUtcNow().ToUnixTimeSeconds();
        now = Math.Max(sample, profile.ObservedAt);
        return sample >= 0 && profile.ObservedAt - sample <= AdministratorAssertion.ClockSkewSeconds;
    }
}
