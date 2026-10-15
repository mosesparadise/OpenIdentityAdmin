using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.AspNetCore.Authentication;

namespace IdentityPlatform.Admin.Bff.Features.Sessions;

/// <summary>Owns immutable lifetime policy and canonical native identity binding; it grants no administrator authority.</summary>
internal static class SessionTicketPolicy
{
    internal const string Scheme = "IdentityPlatform.Admin.Bff.Session";
    private const string SubjectClaim = "sub";
    private const string SessionClaim = "sid";
    private const string InvalidInput = "Invalid session ticket input.";

    /// <summary>Creates full-tick metadata with an absolute ceiling supplied by the source authentication session.</summary>
    internal static AdminSessionTicket Create(string digest, string subject, string sourceSession,
        DateTimeOffset now, DateTimeOffset sourceExpiry, TimeSpan idle, TimeSpan absolute)
    {
        if (!BindingValid(subject) || !BindingValid(sourceSession) ||
            idle <= TimeSpan.Zero || idle > SessionTicketLimits.MaximumIdle ||
            absolute <= TimeSpan.Zero || absolute > SessionTicketLimits.MaximumAbsolute || sourceExpiry <= now)
        {
            throw new ArgumentException(InvalidInput);
        }
        var ceiling = Math.Min(now.UtcTicks + absolute.Ticks, sourceExpiry.UtcTicks);
        return new AdminSessionTicket
        {
            Digest = digest,
            Subject = subject,
            SourceSession = sourceSession,
            CreatedAt = now.UtcTicks,
            LastActivityAt = now.UtcTicks,
            AbsoluteExpiresAt = ceiling,
            IdleLifetime = idle.Ticks,
            IdleExpiresAt = Math.Min(now.UtcTicks + idle.Ticks, ceiling),
            Revision = 1
        };
    }

    /// <summary>Preserves authenticated creation policy while deriving one monotonic successor; no revision wraps.</summary>
    internal static AdminSessionTicket Renew(AdminSessionTicket original, DateTimeOffset now)
    {
        if (!IsLive(original, now) || original.Revision == long.MaxValue)
        {
            throw new ArgumentException(InvalidInput);
        }
        return new AdminSessionTicket
        {
            Digest = original.Digest,
            Subject = original.Subject,
            SourceSession = original.SourceSession,
            CreatedAt = original.CreatedAt,
            LastActivityAt = now.UtcTicks,
            AbsoluteExpiresAt = original.AbsoluteExpiresAt,
            IdleLifetime = original.IdleLifetime,
            IdleExpiresAt = Math.Min(now.UtcTicks + original.IdleLifetime, original.AbsoluteExpiresAt),
            Revision = original.Revision + 1
        };
    }

    /// <summary>Denies equality, revocation and time rollback without changing persisted activity.</summary>
    internal static bool IsLive(AdminSessionTicket row, DateTimeOffset now) =>
        row.RevokedAt is null && row.Revision > 0 && now.UtcTicks >= row.CreatedAt &&
        now.UtcTicks >= row.LastActivityAt && now.UtcTicks < row.IdleExpiresAt && now.UtcTicks < row.AbsoluteExpiresAt;

    /// <summary>Requires exactly one authenticated identity and unique ordinal subject/source claims in the named scheme.</summary>
    internal static bool MatchesBinding(AuthenticationTicket ticket, string subject, string sourceSession)
    {
        const int RejectedIdentityCount = 2;
        var identities = ticket.Principal.Identities.Take(RejectedIdentityCount).ToArray();
        if (ticket.AuthenticationScheme != Scheme || identities.Length != 1 ||
            !identities[0].IsAuthenticated || identities[0].Actor is not null || identities[0].AuthenticationType != Scheme)
        {
            return false;
        }
        var subjects = 0;
        var sessions = 0;
        var inspected = 0;
        foreach (var claim in identities[0].Claims)
        {
            if (++inspected > SessionTicketLimits.NativeBytes)
            {
                return false;
            }
            if (string.Equals(claim.Type, SubjectClaim, StringComparison.OrdinalIgnoreCase))
            {
                if (claim.Type != SubjectClaim || ++subjects != 1 || claim.Value != subject)
                {
                    return false;
                }
            }
            if (string.Equals(claim.Type, SessionClaim, StringComparison.OrdinalIgnoreCase))
            {
                if (claim.Type != SessionClaim || ++sessions != 1 || claim.Value != sourceSession)
                {
                    return false;
                }
            }
        }
        return subjects == 1 && sessions == 1;
    }

    /// <summary>Projects tick-precise eligibility into the native framework's conservative RFC1123 representation.</summary>
    internal static DateTimeOffset NativeTime(long ticks) => new(ticks - ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);

    /// <summary>Rejects empty and oversized opaque bindings without normalization or case folding.</summary>
    private static bool BindingValid(string? value) => !string.IsNullOrEmpty(value) && value.Length <= SessionTicketLimits.BindingLength;
}
