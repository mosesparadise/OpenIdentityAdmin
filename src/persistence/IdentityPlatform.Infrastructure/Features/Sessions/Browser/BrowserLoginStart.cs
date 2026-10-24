using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Browser;

/// <summary>Atomically replaces browser generations, retaining spent state and revoking a former ticket without inferring actor identity.</summary>
internal sealed class BrowserLoginStart(AdminSessionDbContext database, TimeProvider clock, IBrowserTicketAuthenticator authenticator)
{
    private readonly BrowserSessionRows rows = new(database, authenticator);

    /// <summary>Admits one new intent under global quota ordering; the optional actor digest must come from a validated BFF session cookie.</summary>
    internal async Task<long?> StartAsync(string browserDigest, string intentDigest, long expectedGeneration,
        string? authenticatedTicketDigest, CancellationToken token)
    {
        if (!BrowserSessionPolicy.IsDigest(browserDigest) || !BrowserSessionPolicy.IsDigest(intentDigest) ||
            browserDigest == intentDigest || expectedGeneration < 0 || expectedGeneration == long.MaxValue)
        {
            return null;
        }
        await using var owner = await BrowserSessionTransaction.OpenAsync(database, clock, token);
        var browser = await rows.BrowserAsync(browserDigest, owner.Token);
        if (!await AdmittedAsync(browser, expectedGeneration, intentDigest, owner))
        {
            return null;
        }
        var actor = browser is not null && authenticatedTicketDigest is not null
            ? await rows.ActorAsync(browser, authenticatedTicketDigest, owner) : null;
        if (authenticatedTicketDigest is not null && actor is null)
        {
            return null;
        }
        var now = owner.Now;
        if (now <= 0 || browser is not null && now < browser.ObservedAt - BrowserSessionPolicy.Tolerance)
        {
            return null;
        }
        now = Math.Max(now, browser?.ObservedAt ?? now);
        browser ??= NewBrowser(browserDigest);
        if (browser.TicketDigest is { } former)
        {
            await owner.Tickets.RevokeAsync(former, owner.Token);
        }
        await rows.CancelAsync(browser.Digest, owner.Token);
        var intent = Advance(browser, intentDigest, now);
        database.Add(intent);
        owner.Guard(intent.CreatedAt, intent.ObservedAt, intent.ExpiresAt);
        owner.Audit(browser, BrowserMutation.Start, actor);
        return await owner.CommitAsync() ? browser.Generation : null;
    }

    /// <summary>Counts all retained rows and only effective live intents, accounting for the one prior intent cancelled by this replacement.</summary>
    private async Task<bool> AdmittedAsync(BrowserContext? browser, long expected, string intent, BrowserSessionTransaction owner)
    {
        if (browser is null ? expected != 0 : browser.Generation != expected || browser.Revision == long.MaxValue)
        {
            return false;
        }
        if (browser is null && await database.Set<BrowserContext>().CountAsync(owner.Token) >= BrowserSessionPolicy.RetainedLimit)
        {
            return false;
        }
        var intents = database.Set<BrowserLoginIntent>();
        return !await intents.AnyAsync(row => row.Digest == intent, owner.Token) &&
            await intents.CountAsync(owner.Token) < BrowserSessionPolicy.RetainedLimit &&
            await intents.CountAsync(row => row.BrowserDigest != (browser == null ? string.Empty : browser.Digest) &&
                (row.State == BrowserIntentState.Pending || row.State == BrowserIntentState.Reserved) && row.ExpiresAt > owner.Now,
                owner.Token) < BrowserSessionPolicy.LiveLimit;
    }

    /// <summary>Creates a context only for an explicit initial-generation request, never recreating a missing later generation.</summary>
    private BrowserContext NewBrowser(string digest)
    {
        var browser = new BrowserContext { Digest = digest };
        database.Add(browser);
        return browser;
    }

    /// <summary>Advances generation and original operation bounds while retaining any longer former native or ticket dependency.</summary>
    private static BrowserLoginIntent Advance(BrowserContext browser, string digest, long now)
    {
        browser.CorrelationId = Guid.NewGuid();
        browser.Generation = checked(browser.Generation + 1);
        browser.Revision = checked(browser.Revision + 1);
        browser.ObservedAt = now;
        browser.ExpiresAt = checked(now + BrowserSessionPolicy.Lifetime);
        browser.RetainUntil = Math.Max(browser.RetainUntil, checked(browser.ExpiresAt + BrowserSessionPolicy.Tolerance));
        browser.TicketDigest = null;
        return new BrowserLoginIntent
        {
            Digest = digest,
            BrowserDigest = browser.Digest,
            CorrelationId = browser.CorrelationId,
            Generation = browser.Generation,
            Revision = 1,
            CreatedAt = now,
            ObservedAt = now,
            ExpiresAt = browser.ExpiresAt,
            RetainUntil = checked(browser.ExpiresAt + BrowserSessionPolicy.Tolerance),
            State = BrowserIntentState.Pending
        };
    }
}
