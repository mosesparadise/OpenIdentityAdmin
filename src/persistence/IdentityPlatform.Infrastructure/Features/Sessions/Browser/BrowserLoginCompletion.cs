using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Browser;

/// <summary>Reserves once and atomically establishes a prepared canonical ticket in the winning browser generation.</summary>
internal sealed class BrowserLoginCompletion(AdminSessionDbContext database, TimeProvider clock, IBrowserTicketAuthenticator authenticator)
{
    private readonly BrowserSessionRows rows = new(database, authenticator);

    /// <summary>Spends pending intent before native HTTP; a reserved or uncertain result never authorizes another challenge.</summary>
    internal async Task<bool> ReserveAsync(string browserDigest, string intentDigest, long generation, string reservationDigest, CancellationToken token)
    {
        if (!Valid(browserDigest, intentDigest, generation) || !BrowserSessionPolicy.IsDigest(reservationDigest) ||
            reservationDigest == browserDigest || reservationDigest == intentDigest)
        {
            return false;
        }
        await using var owner = await BrowserSessionTransaction.OpenAsync(database, clock, token);
        var browser = await rows.BrowserAsync(browserDigest, owner.Token);
        if (browser is null || !BrowserSessionPolicy.Matches(browser, generation, owner.Now))
        {
            return false;
        }
        var intent = await rows.IntentAsync(intentDigest, browser, BrowserIntentState.Pending, owner);
        if (intent is null || await database.Set<BrowserLoginIntent>().AnyAsync(row => row.NativeStateDigest == reservationDigest, owner.Token))
        {
            return false;
        }
        intent.NativeStateDigest = reservationDigest;
        intent.State = BrowserIntentState.Reserved;
        intent.Revision = checked(intent.Revision + 1);
        intent.RetainUntil = Math.Max(intent.RetainUntil, checked(intent.ExpiresAt + BrowserSessionPolicy.Lifetime + BrowserSessionPolicy.Tolerance));
        browser.RetainUntil = Math.Max(browser.RetainUntil, intent.RetainUntil);
        Observe(browser, owner.Now);
        owner.Audit(browser, BrowserMutation.Reserve);
        return await owner.CommitAsync();
    }

    /// <summary>Requires the callback owner's exact protected reservation and prepared ticket; persistence does not replace native validation/consumption.</summary>
    internal async Task<bool> EstablishAsync(string browserDigest, string intentDigest, long generation, string reservationDigest,
        AdminSessionTicket prepared, CancellationToken token)
    {
        var ticket = BrowserSessionRows.Snapshot(prepared);
        if (!Valid(browserDigest, intentDigest, generation) || !BrowserSessionPolicy.IsDigest(reservationDigest) ||
            !BrowserSessionPolicy.IsDigest(ticket.Digest) || ticket.Revision != 1 || !authenticator.IsAuthentic(ticket, clock.GetUtcNow()))
        {
            return false;
        }
        await using var owner = await BrowserSessionTransaction.OpenAsync(database, clock, token);
        var browser = await rows.BrowserAsync(browserDigest, owner.Token);
        if (browser is null || browser.TicketDigest is not null || !BrowserSessionPolicy.Matches(browser, generation, owner.Now))
        {
            return false;
        }
        var intent = await rows.IntentAsync(intentDigest, browser, BrowserIntentState.Reserved, owner);
        if (intent is null || intent.NativeStateDigest != reservationDigest || !authenticator.IsAuthentic(ticket, clock.GetUtcNow()) ||
            !await owner.Tickets.CreateAsync(ticket, owner.Token))
        {
            return false;
        }
        intent.State = BrowserIntentState.Established;
        intent.TicketDigest = ticket.Digest;
        intent.Revision = checked(intent.Revision + 1);
        intent.RetainUntil = Math.Max(intent.RetainUntil, checked(ticket.AbsoluteExpiresAt + BrowserSessionPolicy.Tolerance));
        browser.TicketDigest = ticket.Digest;
        browser.ExpiresAt = ticket.AbsoluteExpiresAt;
        browser.RetainUntil = Math.Max(browser.RetainUntil, intent.RetainUntil);
        Observe(browser, owner.Now);
        owner.Audit(browser, BrowserMutation.Establish, ticket);
        return await owner.CommitAsync();
    }

    /// <summary>Terminates a failed reserved exchange without retrying or releasing its original native retention obligation.</summary>
    internal async Task<bool> DenyAsync(string browserDigest, string intentDigest, long generation, CancellationToken token)
    {
        if (!Valid(browserDigest, intentDigest, generation))
        {
            return false;
        }
        await using var owner = await BrowserSessionTransaction.OpenAsync(database, clock, token);
        var browser = await rows.BrowserAsync(browserDigest, owner.Token);
        if (browser is null || !BrowserSessionPolicy.Matches(browser, generation, owner.Now))
        {
            return false;
        }
        var intent = await rows.IntentAsync(intentDigest, browser, BrowserIntentState.Reserved, owner);
        if (intent is null)
        {
            return false;
        }
        intent.State = BrowserIntentState.Denied;
        intent.Revision = checked(intent.Revision + 1);
        Observe(browser, owner.Now);
        owner.Audit(browser, BrowserMutation.Deny);
        return await owner.CommitAsync();
    }

    /// <summary>Performs exact current-generation lookup on every product acceptance; absence and stale generations deny without renewing.</summary>
    internal async Task<bool> IsCurrentAsync(string browserDigest, long generation, string ticketDigest, CancellationToken token)
    {
        if (!BrowserSessionPolicy.IsDigest(browserDigest) || !BrowserSessionPolicy.IsDigest(ticketDigest))
        {
            return false;
        }
        var row = await database.Set<BrowserContext>().AsNoTracking().SingleOrDefaultAsync(item => item.Digest == browserDigest, token);
        return row is not null && row.TicketDigest == ticketDigest && BrowserSessionPolicy.Matches(row, generation, clock.GetUtcNow().UtcTicks);
    }

    /// <summary>Rejects malformed lookup bindings before any transaction is opened.</summary>
    private static bool Valid(string browser, string intent, long generation) => generation > 0 &&
        BrowserSessionPolicy.IsDigest(browser) && BrowserSessionPolicy.IsDigest(intent) && browser != intent;

    /// <summary>Advances only observation and revision; operation deadlines are never renewed by progress.</summary>
    private static void Observe(BrowserContext browser, long now)
    {
        browser.ObservedAt = Math.Max(browser.ObservedAt, now);
        browser.Revision = checked(browser.Revision + 1);
    }
}
