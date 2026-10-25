using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Separates absent or expired authentication from live foreign authority before browser mutations.</summary>
internal sealed class BrowserSessionActor(AdminSessionDbContext database, TimeProvider clock, SessionTicketCodec codec)
{
    /// <summary>Accepts an audit actor only from a live authentic B1 envelope and the exact current browser/ticket association.</summary>
    internal async Task<(bool Allowed, string? Digest)> ReadAsync(BrowserGenerationProof browser, string? handle, CancellationToken token)
    {
        if (handle is null)
        {
            return (true, null);
        }
        if (!SessionHandle.TryDigest(handle, out var digest))
        {
            return (false, null);
        }
        var ticket = await database.Set<AdminSessionTicket>().AsNoTracking().SingleOrDefaultAsync(row => row.Digest == digest, token);
        if (ticket is null || !codec.IsAuthentic(ticket, clock.GetUtcNow()))
        {
            return (true, null);
        }
        var context = await database.Set<BrowserContext>().AsNoTracking().SingleOrDefaultAsync(row => row.Digest == browser.Digest, token);
        return BrowserTicketAcceptance.Accept(handle, browser.Digest, browser.Generation, context, ticket, codec, clock.GetUtcNow())
            ? (true, digest) : (false, null);
    }

}
