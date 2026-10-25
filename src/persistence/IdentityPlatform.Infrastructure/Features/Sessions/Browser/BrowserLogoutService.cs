using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Browser;

/// <summary>Commits local invalidation independently of any issuer request and records remote navigation without attesting its outcome.</summary>
internal sealed class BrowserLogoutService(AdminSessionDbContext database, TimeProvider clock, IBrowserTicketAuthenticator authenticator)
{
    private readonly BrowserSessionRows rows = new(database, authenticator);

    /// <summary>Invalidates the expected generation and ticket atomically; remote correlation requires the caller's exact authenticated ticket.</summary>
    internal async Task<BrowserLogoutResult?> LogoutAsync(string browserDigest, long expectedGeneration, string? authenticatedTicketDigest,
        string? correlationDigest, CancellationToken token)
    {
        if (!BrowserSessionPolicy.IsDigest(browserDigest) || expectedGeneration <= 0 || expectedGeneration == long.MaxValue ||
            correlationDigest is not null && (!BrowserSessionPolicy.IsDigest(correlationDigest) || correlationDigest == browserDigest))
        {
            return null;
        }
        await using var owner = await BrowserSessionTransaction.OpenAsync(database, clock, token);
        var browser = await rows.BrowserAsync(browserDigest, owner.Token);
        if (browser is null || browser.Generation != expectedGeneration || owner.Now < browser.ObservedAt - BrowserSessionPolicy.Tolerance)
        {
            return null;
        }
        var actor = authenticatedTicketDigest is null ? null : await rows.ActorAsync(browser, authenticatedTicketDigest, owner);
        if (authenticatedTicketDigest is not null && actor is null || correlationDigest is not null && actor is null)
        {
            return null;
        }
        var remote = correlationDigest is not null && await CapacityAsync(correlationDigest, owner);
        if (browser.TicketDigest is { } ticket)
        {
            await owner.Tickets.RevokeAsync(ticket, owner.Token);
        }
        await rows.CancelAsync(browser.Digest, owner.Token);
        Advance(browser, owner.Now);
        var continuation = remote ? Correlation(browser, correlationDigest!, actor!) : null;
        if (continuation is not null)
        {
            database.Add(continuation);
        }
        owner.Guard(browser.ObservedAt, browser.ObservedAt, browser.ExpiresAt);
        owner.Audit(browser, BrowserMutation.Logout, actor);
        return await owner.CommitAsync() ? new BrowserLogoutResult(browser.Generation, remote, browser.ObservedAt, browser.ExpiresAt, continuation is null ? null : BrowserLogoutReservation.Capture(continuation)) : null;
    }

    /// <summary>Transitions only the original one-use correlation; a browser return never authenticates or claims issuer revocation.</summary>
    internal async Task<bool> RemoteAsync(string browserDigest, string correlationDigest, long generation, bool returned, CancellationToken token) =>
        await TransitionAsync(browserDigest, correlationDigest, generation, returned, token) is not null;

    /// <summary>Returns only a detached snapshot after the exact original correlation transition commits and its transaction disposes.</summary>
    internal async Task<BrowserLogoutReservation?> TransitionAsync(string browserDigest, string correlationDigest, long generation, bool returned,
        CancellationToken token, long? created = null, long? expires = null)
    {
        if (!BrowserSessionPolicy.IsDigest(browserDigest) || !BrowserSessionPolicy.IsDigest(correlationDigest) || generation <= 0)
        {
            return null;
        }
        await using var owner = await BrowserSessionTransaction.OpenAsync(database, clock, token);
        var browser = await rows.BrowserAsync(browserDigest, owner.Token);
        var row = await database.Set<BrowserLogoutContinuation>().SingleOrDefaultAsync(item => item.Digest == correlationDigest, owner.Token);
        var expected = returned ? BrowserLogoutState.RemotePending : BrowserLogoutState.LocalCommitted;
        if (browser is null || !BrowserSessionPolicy.Matches(browser, generation, owner.Now) || row is null || row.State != expected ||
            row.BrowserDigest != browserDigest || row.Generation != generation || row.CorrelationId != browser.CorrelationId ||
            created is { } original && row.CreatedAt != original || expires is { } deadline && row.ExpiresAt != deadline ||
            !BrowserSessionPolicy.Current(row.CreatedAt, row.ObservedAt, row.ExpiresAt, owner.Now))
        {
            return null;
        }
        row.State = returned ? BrowserLogoutState.ReturnedUnverified : BrowserLogoutState.RemotePending;
        row.Revision = checked(row.Revision + 1);
        row.ObservedAt = Math.Max(row.ObservedAt, owner.Now);
        browser.ObservedAt = Math.Max(browser.ObservedAt, row.ObservedAt);
        browser.Revision = checked(browser.Revision + 1);
        owner.Guard(row.CreatedAt, row.ObservedAt, row.ExpiresAt);
        owner.Audit(browser, returned ? BrowserMutation.Return : BrowserMutation.RemotePending);
        return await owner.CommitAsync() ? BrowserLogoutReservation.Capture(row) : null;
    }

    /// <summary>Denies at either ceiling without evicting retained markers or turning quota pressure into an unaudited partial logout.</summary>
    private async Task<bool> CapacityAsync(string digest, BrowserSessionTransaction owner)
    {
        var set = database.Set<BrowserLogoutContinuation>();
        var now = owner.Now;
        return !await set.AnyAsync(row => row.Digest == digest, owner.Token) &&
            await set.CountAsync(owner.Token) < BrowserSessionPolicy.RetainedLimit &&
            await set.CountAsync(row => row.ExpiresAt > now && (row.State == BrowserLogoutState.LocalCommitted ||
                row.State == BrowserLogoutState.RemotePending), owner.Token) < BrowserSessionPolicy.LiveLimit;
    }

    /// <summary>Records a new tombstoned generation while retaining any longer old dependency and clearing the current ticket reference.</summary>
    private static void Advance(BrowserContext browser, long observed)
    {
        browser.CorrelationId = Guid.NewGuid();
        browser.Generation = checked(browser.Generation + 1);
        browser.Revision = checked(browser.Revision + 1);
        browser.ObservedAt = Math.Max(browser.ObservedAt, observed);
        browser.ExpiresAt = checked(browser.ObservedAt + BrowserSessionPolicy.Lifetime);
        browser.RetainUntil = Math.Max(browser.RetainUntil, checked(browser.ExpiresAt + BrowserSessionPolicy.Tolerance));
        browser.TicketDigest = null;
    }

    /// <summary>Stores source selectors from validated ticket provenance, never from query or form input.</summary>
    private static BrowserLogoutContinuation Correlation(BrowserContext browser, string digest, AdminSessionTicket actor) => new()
    {
        Digest = digest,
        BrowserDigest = browser.Digest,
        CorrelationId = browser.CorrelationId,
        Generation = browser.Generation,
        Revision = 1,
        CreatedAt = browser.ObservedAt,
        ObservedAt = browser.ObservedAt,
        ExpiresAt = browser.ExpiresAt,
        RetainUntil = checked(browser.ExpiresAt + BrowserSessionPolicy.Tolerance),
        State = BrowserLogoutState.LocalCommitted,
        Subject = actor.Subject,
        SourceSession = actor.SourceSession
    };
}

/// <summary>Separates committed local invalidation from optional remote capacity, without claiming any issuer outcome.</summary>
internal sealed class BrowserLogoutResult(long generation, bool remoteReserved, long observedAt, long expiresAt, BrowserLogoutReservation? remote = null)
{
    internal long Generation { get; } = generation;
    internal bool RemoteReserved { get; } = remoteReserved;
    internal BrowserLogoutReservation? Remote { get; } = remote;
    internal long ObservedAt { get; } = observedAt;
    internal long ExpiresAt { get; } = expiresAt;
}
