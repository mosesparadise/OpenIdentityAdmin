using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Browser;

/// <summary>Lets the owning BFF authenticate protected ticket metadata without moving its key material into persistence.</summary>
internal interface IBrowserTicketAuthenticator
{
    /// <summary>Requires the exact envelope and current B1 lifetime; this is not native callback-consumption authority.</summary>
    bool IsAuthentic(AdminSessionTicket row, DateTimeOffset now);
}
