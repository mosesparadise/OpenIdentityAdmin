using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Browser;

/// <summary>Recognizes only the immediately preceding committed local logout without advancing state or renewing its deadline.</summary>
internal sealed class BrowserLogoutReconciliation(AdminSessionDbContext database, TimeProvider clock)
{
    /// <summary>Rechecks under ordinary writer ordering, then rolls back acquisition itself before the caller can publish a confirmation.</summary>
    internal async Task<BrowserLogoutResult?> ReconcileAsync(string digest, long previousGeneration, CancellationToken token)
    {
        await using var owner = await BrowserSessionTransaction.OpenAsync(database, clock, token);
        return await ReadAsync(digest, previousGeneration, owner.Token);
    }

    /// <summary>Admits an inert form only for the exact current logout tombstone, never a replacement, authenticated generation or pending ceremony.</summary>
    internal async Task<BrowserLogoutResult?> ReadAsync(string digest, long previousGeneration, CancellationToken token)
    {
        if (!BrowserSessionPolicy.IsDigest(digest) || previousGeneration <= 0 || previousGeneration == long.MaxValue)
        {
            return null;
        }
        var row = await database.Set<BrowserContext>().AsNoTracking().SingleOrDefaultAsync(value => value.Digest == digest, token);
        if (row is null || row.TicketDigest is not null || !BrowserSessionPolicy.Matches(row, previousGeneration + 1, clock.GetUtcNow().UtcTicks) ||
            !BrowserSessionPolicy.Current(row.ObservedAt, row.ObservedAt, row.ExpiresAt, clock.GetUtcNow().UtcTicks) ||
            await database.Set<BrowserLoginIntent>().AsNoTracking().AnyAsync(value => value.BrowserDigest == digest &&
                (value.State == BrowserIntentState.Pending || value.State == BrowserIntentState.Reserved), token) ||
            await database.Set<BrowserSessionAudit>().AsNoTracking().CountAsync(value => value.CorrelationId == row.CorrelationId &&
                value.Generation == row.Generation && value.Operation == BrowserMutation.Logout, token) != 1)
        {
            return null;
        }
        return new(row.Generation, false, row.ObservedAt, row.ExpiresAt);
    }
}
