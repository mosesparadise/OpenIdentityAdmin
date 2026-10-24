using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Browser;

/// <summary>Reclaims only finite expired children then unreferenced contexts under the same ordering as every browser writer.</summary>
internal sealed class BrowserSessionCleanup(AdminSessionDbContext database, TimeProvider clock)
{
    /// <summary>Removes at most one hundred records per category; failure rolls back the whole batch and never grants authentication authority.</summary>
    internal async Task<int> RunAsync(CancellationToken token)
    {
        await using var owner = await BrowserSessionTransaction.OpenAsync(database, clock, token);
        var now = owner.Now;
        owner.GuardCleanup(now);
        var intents = await database.Set<BrowserLoginIntent>().Where(row => row.RetainUntil < now && row.ObservedAt <= now &&
            !database.Set<AdminSessionTicket>().Any(ticket => ticket.Digest == row.TicketDigest && ticket.RevokedAt == null &&
                ticket.IdleExpiresAt > now && ticket.AbsoluteExpiresAt > now))
            .OrderBy(row => row.RetainUntil).ThenBy(row => row.Digest).Take(BrowserSessionPolicy.CleanupLimit).ToListAsync(owner.Token);
        var logouts = await database.Set<BrowserLogoutContinuation>().Where(row => row.RetainUntil < now && row.ObservedAt <= now)
            .OrderBy(row => row.RetainUntil).ThenBy(row => row.Digest).Take(BrowserSessionPolicy.CleanupLimit).ToListAsync(owner.Token);
        database.RemoveRange(intents);
        database.RemoveRange(logouts);
        await database.SaveChangesAsync(owner.Token);
        var contexts = await database.Set<BrowserContext>().Where(row => row.RetainUntil < now && row.ObservedAt <= now &&
            !database.Set<BrowserLoginIntent>().Any(intent => intent.BrowserDigest == row.Digest) &&
            !database.Set<BrowserLogoutContinuation>().Any(logout => logout.BrowserDigest == row.Digest) &&
            !database.Set<AdminSessionTicket>().Any(ticket => ticket.Digest == row.TicketDigest && ticket.RevokedAt == null &&
                ticket.IdleExpiresAt > now && ticket.AbsoluteExpiresAt > now))
            .OrderBy(row => row.RetainUntil).ThenBy(row => row.Digest).Take(BrowserSessionPolicy.CleanupLimit).ToListAsync(owner.Token);
        database.RemoveRange(contexts);
        return await owner.CommitAsync() ? intents.Count + logouts.Count + contexts.Count : 0;
    }
}
