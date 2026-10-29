using System.Text.Json.Serialization;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Exposes only local authentication and its effective deadline; never represents current API permission.</summary>
internal sealed record BrowserSessionStatus(bool Authenticated,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Subject = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? ExpiresAt = null);

/// <summary>Reads the protected ticket and current browser association together without updating activity, generation or cookies.</summary>
internal sealed class BrowserSessionReader(AdminSessionDbContext database, BrowserLoginProof proofs, SessionTicketCodec codec, TimeProvider clock)
{
    /// <summary>Retains detached original state for final expiry checks after the owning scope is disposed; unavailable storage propagates.</summary>
    internal async Task<BrowserSessionRead?> ReadAsync(HttpContext http)
    {
        if (!BrowserLoginRequest.Cookie(http.Request, BrowserSessionCookies.Session, out var handle) ||
            !BrowserLoginRequest.Cookie(http.Request, BrowserSessionCookies.Generation, out var raw) ||
            !SessionHandle.TryDigest(handle, out var digest) || proofs.Generation(raw, clock.GetUtcNow()) is not { } proof)
        {
            return null;
        }
        var pair = await (from browser in database.Set<BrowserContext>().AsNoTracking()
                          join ticket in database.Set<AdminSessionTicket>().AsNoTracking() on browser.TicketDigest equals ticket.Digest
                          where browser.Digest == proof.Digest && ticket.Digest == digest
                          select new
                          {
                              Browser = browser,
                              Ticket = ticket
                          }).SingleOrDefaultAsync(http.RequestAborted);
        return pair is null || !BrowserTicketAcceptance.Accept(handle, proof.Digest, proof.Generation, pair.Browser, pair.Ticket, codec, clock.GetUtcNow())
            ? null : new BrowserSessionRead(proof, pair.Browser, pair.Ticket);
    }
}

/// <summary>Freezes an already authenticated read; later concurrent revocation does not promise cancellation of admitted reads.</summary>
internal sealed class BrowserSessionRead(BrowserGenerationProof proof, BrowserContext browser, AdminSessionTicket ticket)
{
    /// <summary>Revalidates the original browser deadline and protected row before releasing a server-only credential for the fixed API.</summary>
    internal BrowserAccessCredential? Access(SessionTicketCodec codec, DateTimeOffset now) =>
        Status(now).Authenticated && codec.Unprotect(ticket) is { } native ? BrowserAccessCredential.Read(native.Properties, now) : null;

    /// <summary>Applies the original protected, durable and ticket deadlines again immediately before publication without renewal.</summary>
    internal BrowserSessionStatus Status(DateTimeOffset now) =>
        now.UtcTicks >= proof.Created && now.UtcTicks < proof.Expires &&
        BrowserSessionPolicy.Matches(browser, proof.Generation, now.UtcTicks) && SessionTicketPolicy.IsLive(ticket, now)
            ? new(true, ticket.Subject, new DateTimeOffset(Math.Min(ticket.IdleExpiresAt, Math.Min(ticket.AbsoluteExpiresAt,
                Math.Min(browser.ExpiresAt, proof.Expires))), TimeSpan.Zero)) : new(false);
}
