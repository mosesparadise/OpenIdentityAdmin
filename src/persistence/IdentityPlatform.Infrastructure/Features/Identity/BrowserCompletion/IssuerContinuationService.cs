using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;

/// <summary>Admits bounded anonymous continuation state under the existing installation admission lock, without admitting any account.</summary>
internal sealed class IssuerContinuationService(PlatformDbContext database, NativeOperation operation, CeremonyClock clock)
{
    /// <summary>Commits one original intent and anonymous audit; duplicates, browser contention and either capacity ceiling deny unchanged.</summary>
    internal Task<bool> StartAsync(IssuerContinuationRequest request, CancellationToken token) => StartCoreAsync(request, null, token);

    /// <summary>Reconciles only the same pristine pending admission for an explicit password submission, preserving its authenticated entry deadline.</summary>
    internal Task<bool> AdmitForBrowserAsync(IssuerContinuationRequest request, long entryDeadline, CancellationToken token) =>
        StartCoreAsync(request, entryDeadline, token);

    /// <summary>Owns initial admission or an unchanged pending read; later states never become a password retry.</summary>
    private async Task<bool> StartCoreAsync(IssuerContinuationRequest request, long? entryDeadline, CancellationToken token)
    {
        await using var transaction = await CeremonyTransaction.OpenContinuationAsync(database, operation, token);
        var rows = database.Set<IssuerContinuation>();
        var now = clock.Now;
        var existing = await rows.SingleOrDefaultAsync(row => row.IntentDigest == request.IntentDigest, transaction.Token);
        if (existing is not null)
        {
            return entryDeadline is { } original && existing.ExpiresAt == original && existing.BrowserDigest == request.BrowserDigest &&
                IssuerContinuationPolicy.IsStart(existing) && IssuerContinuationPolicy.IsCurrent(existing, now);
        }
        var expires = entryDeadline ?? checked(now + IssuerContinuationPolicy.LifetimeSeconds);
        if (now <= 0 || expires <= now || expires - now > IssuerContinuationPolicy.LifetimeSeconds ||
            await rows.CountAsync(transaction.Token) >= IssuerContinuationPolicy.RetainedLimit ||
            await rows.CountAsync(row => row.State != IssuerContinuationPolicy.Attached && row.ExpiresAt > now, transaction.Token) >= IssuerContinuationPolicy.LiveLimit ||
            await rows.AnyAsync(row => row.BrowserDigest == request.BrowserDigest && row.State != IssuerContinuationPolicy.Attached && row.ExpiresAt > now, transaction.Token))
        {
            return false;
        }
        var row = new IssuerContinuation
        {
            IntentDigest = request.IntentDigest,
            BrowserDigest = request.BrowserDigest,
            ClientId = AdministratorProtocolIdentity.ClientId,
            CreatedAt = now,
            ObservedAt = now,
            ExpiresAt = expires,
            RetainUntil = checked(expires + IssuerContinuationPolicy.ClockToleranceSeconds),
            Revision = 1,
            CorrelationId = Guid.NewGuid()
        };
        database.Add(row);
        if (!IssuerContinuationPolicy.Observe(row, clock.Now))
        {
            throw new FactorUnavailableException();
        }
        await transaction.CommitContinuationAsync(row, clock);
        return true;
    }

    /// <summary>Prunes only a finite eligible batch after every original dependency deadline; cleanup grants no ceremony authority.</summary>
    internal async Task<int> CleanupAsync(CancellationToken token)
    {
        await using var transaction = await CeremonyTransaction.OpenMaintenanceAsync(database, operation,
            new CeremonyLockPlan(false, true, [], []), token);
        var now = clock.Now;
        var rows = await database.Set<IssuerContinuation>().Where(row => row.RetainUntil < now && row.ObservedAt <= now)
            .OrderBy(row => row.RetainUntil).ThenBy(row => row.IntentDigest).Take(IssuerContinuationPolicy.CleanupLimit).ToListAsync(transaction.Token);
        database.RemoveRange(rows);
        await transaction.CommitMaintenanceAsync();
        return rows.Count;
    }
}
