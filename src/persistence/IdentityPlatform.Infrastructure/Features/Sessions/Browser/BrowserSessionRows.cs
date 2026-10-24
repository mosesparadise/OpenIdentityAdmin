using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Browser;

/// <summary>Reads exact owned bindings and captures caller material before any asynchronous persistence work.</summary>
internal sealed class BrowserSessionRows(AdminSessionDbContext database, IBrowserTicketAuthenticator authenticator)
{
    /// <summary>Looks up one canonical anonymous browser proof; it supplies ordering only, not account authority.</summary>
    internal Task<BrowserContext?> BrowserAsync(string digest, CancellationToken token) =>
        database.Set<BrowserContext>().SingleOrDefaultAsync(row => row.Digest == digest, token);

    /// <summary>Requires the exact intent, browser, generation, client and original lifetime under acquired writer ordering.</summary>
    internal async Task<BrowserLoginIntent?> IntentAsync(string digest, BrowserContext browser, BrowserIntentState state,
        BrowserSessionTransaction owner)
    {
        var row = await database.Set<BrowserLoginIntent>().SingleOrDefaultAsync(item => item.Digest == digest, owner.Token);
        if (row is null || row.BrowserDigest != browser.Digest || row.Generation != browser.Generation || row.State != state ||
            row.CorrelationId != browser.CorrelationId || row.CorrelationId == Guid.Empty ||
            row.Revision <= 0 || row.ClientId != AdministratorProtocolIdentity.ClientId ||
            !BrowserSessionPolicy.Current(row.CreatedAt, row.ObservedAt, row.ExpiresAt, owner.Now))
        {
            return null;
        }
        row.ObservedAt = Math.Max(row.ObservedAt, owner.Now);
        owner.Guard(row.CreatedAt, row.ObservedAt, row.ExpiresAt);
        return row;
    }

    /// <summary>Authenticates only the exact current ticket selected by the already validated BFF cookie, never a browser proof alone.</summary>
    internal async Task<AdminSessionTicket?> ActorAsync(BrowserContext browser, string authenticatedDigest, BrowserSessionTransaction owner)
    {
        if (browser.TicketDigest != authenticatedDigest || !BrowserSessionPolicy.IsDigest(authenticatedDigest))
        {
            return null;
        }
        var row = await database.Set<AdminSessionTicket>().AsNoTracking().SingleOrDefaultAsync(item => item.Digest == authenticatedDigest, owner.Token);
        return row is not null && authenticator.IsAuthentic(row, new(owner.Now, TimeSpan.Zero)) ? row : null;
    }

    /// <summary>Copies protected bytes and every authenticated B1 field before caller-owned state can change during a wait.</summary>
    internal static AdminSessionTicket Snapshot(AdminSessionTicket row) => new()
    {
        Digest = row.Digest,
        Subject = row.Subject,
        SourceSession = row.SourceSession,
        ProtectedTicket = row.ProtectedTicket.ToArray(),
        CreatedAt = row.CreatedAt,
        LastActivityAt = row.LastActivityAt,
        AbsoluteExpiresAt = row.AbsoluteExpiresAt,
        IdleExpiresAt = row.IdleExpiresAt,
        IdleLifetime = row.IdleLifetime,
        Revision = row.Revision,
        RevokedAt = row.RevokedAt
    };

    /// <summary>Cancels only this context's still pending/reserved work; established and terminal history stays intact.</summary>
    internal Task<int> CancelAsync(string browser, CancellationToken token) => database.Set<BrowserLoginIntent>()
        .Where(row => row.BrowserDigest == browser && (row.State == BrowserIntentState.Pending || row.State == BrowserIntentState.Reserved))
        .ExecuteUpdateAsync(update => update.SetProperty(row => row.State, BrowserIntentState.Cancelled)
            .SetProperty(row => row.Revision, row => row.Revision + 1), token);
}
