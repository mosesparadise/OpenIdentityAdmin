using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Reads original browser proofs and reserved durable provenance after native state consumption, before token exchange.</summary>
internal sealed class BrowserCallbackAdmission(BrowserGenerationProof browser, BrowserPendingProof pending, string reservation)
{
    internal BrowserGenerationProof Browser { get; } = browser;
    internal BrowserPendingProof Pending { get; } = pending;
    internal string Reservation { get; } = reservation;
    internal BrowserLoginPublication Publication { get; } = new(pending.Created, pending.Expires, new(browser.Created, browser.Expires));

    /// <summary>Uses a fresh bounded local read scope and disposes it before returning the immutable expected tuple.</summary>
    internal static async Task<BrowserCallbackAdmission> ReadAsync(HttpContext http, IServiceScopeFactory factory, TimeProvider clock)
    {
        await using var scope = factory.CreateAsyncScope();
        var proofs = scope.ServiceProvider.GetRequiredService<BrowserLoginProof>();
        if (!BrowserLoginRequest.Cookie(http.Request, BrowserSessionCookies.Generation, out var rawBrowser) ||
            !BrowserLoginRequest.Cookie(http.Request, BrowserSessionCookies.Prelogin, out var rawPending) ||
            proofs.Generation(rawBrowser, clock.GetUtcNow()) is not { } browser ||
            proofs.Pending(rawPending, clock.GetUtcNow()) is not { } pending ||
            pending.BrowserDigest != browser.Digest || pending.Generation != browser.Generation)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var database = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        var context = await database.Set<BrowserContext>().AsNoTracking().SingleOrDefaultAsync(row => row.Digest == browser.Digest, http.RequestAborted);
        var intent = await database.Set<BrowserLoginIntent>().AsNoTracking().SingleOrDefaultAsync(row => row.Digest == pending.Digest, http.RequestAborted);
        if (context is null || context.TicketDigest is not null || !BrowserSessionPolicy.Matches(context, browser.Generation, clock.GetUtcNow().UtcTicks) ||
            intent is null || intent.State != BrowserIntentState.Reserved || intent.BrowserDigest != browser.Digest || intent.Generation != browser.Generation ||
            intent.ClientId != AdministratorProtocolIdentity.ClientId || intent.CorrelationId != context.CorrelationId ||
            intent.CorrelationId == Guid.Empty || intent.Revision <= 0 ||
            intent.CreatedAt != pending.Created || intent.ExpiresAt != pending.Expires || !BrowserSessionPolicy.IsDigest(intent.NativeStateDigest) ||
            !BrowserSessionPolicy.Current(intent.CreatedAt, intent.ObservedAt, intent.ExpiresAt, clock.GetUtcNow().UtcTicks))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return new(browser, pending, intent.NativeStateDigest!);
    }
}
