using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Combines protected B1 authentication with a separately loaded current generation without changing activity or treating lookup as authority.</summary>
internal static class BrowserTicketAcceptance
{
    /// <summary>Requires a canonical handle, exact browser/ticket association and real B1 envelope/lifetime; storage failures remain the async owner's unavailable outcome.</summary>
    internal static bool Accept(string? handle, string browserDigest, long generation, BrowserContext? browser,
        AdminSessionTicket? ticket, SessionTicketCodec codec, DateTimeOffset now) =>
        SessionHandle.TryDigest(handle, out var digest) && BrowserSessionPolicy.IsDigest(browserDigest) &&
        browser is not null && ticket is not null && browser.Digest == browserDigest && browser.TicketDigest == digest &&
        ticket.Digest == digest && BrowserSessionPolicy.Matches(browser, generation, now.UtcTicks) && codec.IsAuthentic(ticket, now);
}
