using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;

namespace IdentityPlatform.Admin.Bff.Features.Sessions;

/// <summary>Protects native tickets together with every authentication-relevant row field and revalidates both representations.</summary>
internal sealed class SessionTicketCodec(IDataProtectionProvider provider) : IBrowserTicketAuthenticator
{
    private const string Purpose = "IdentityPlatform.Admin.Bff.SessionTicket.v1";
    private const string InvalidTicket = "Invalid session ticket material.";
    private const int EnvelopeVersion = 1;
    private readonly IDataProtector protector = provider.CreateProtector(Purpose);

    /// <summary>Authenticates the owned row and its unchanged B1 lifetime without granting browser or native callback authority.</summary>
    public bool IsAuthentic(AdminSessionTicket row, DateTimeOffset now) =>
        Unprotect(row) is not null && SessionTicketPolicy.IsLive(row, now);

    /// <summary>Serializes a private native copy, overriding only store-owned validity fields before authenticating its envelope.</summary>
    internal byte[] Protect(AdminSessionTicket row, AuthenticationTicket ticket)
    {
        if (!SessionTicketPolicy.MatchesBinding(ticket, row.Subject, row.SourceSession))
        {
            throw new ArgumentException(InvalidTicket);
        }
        var native = BoundedTicketSerialization.Serialize(ticket);
        RequireNativeSize(native);
        var copy = TicketSerializer.Default.Deserialize(native) ?? throw new ArgumentException(InvalidTicket);
        copy.Properties.IssuedUtc = SessionTicketPolicy.NativeTime(row.LastActivityAt);
        copy.Properties.ExpiresUtc = SessionTicketPolicy.NativeTime(row.IdleExpiresAt);
        native = BoundedTicketSerialization.Serialize(copy);
        RequireNativeSize(native);
        var serialized = JsonSerializer.SerializeToUtf8Bytes(new Envelope { Version = EnvelopeVersion, State = row, Ticket = native });
        var protectedBytes = protector.Protect(serialized);
        if (protectedBytes.Length > SessionTicketLimits.ProtectedBytes)
        {
            throw new ArgumentException(InvalidTicket);
        }
        return protectedBytes;
    }

    /// <summary>Rejects oversized, substituted or malformed material before returning any native claims or tokens.</summary>
    internal AuthenticationTicket? Unprotect(AdminSessionTicket row)
    {
        if (row.ProtectedTicket.Length is 0 or > SessionTicketLimits.ProtectedBytes)
        {
            return null;
        }
        try
        {
            var bytes = protector.Unprotect(row.ProtectedTicket);
            if (bytes.Length > SessionTicketLimits.ProtectedBytes)
            {
                return null;
            }
            var envelope = JsonSerializer.Deserialize<Envelope>(bytes);
            if (envelope?.Version != EnvelopeVersion || envelope.State is null ||
                envelope.Ticket is null || !Matches(row, envelope.State))
            {
                return null;
            }
            RequireNativeSize(envelope.Ticket);
            var ticket = TicketSerializer.Default.Deserialize(envelope.Ticket);
            return ticket is not null && SessionTicketPolicy.MatchesBinding(ticket, row.Subject, row.SourceSession) &&
                ticket.Properties.IssuedUtc == SessionTicketPolicy.NativeTime(row.LastActivityAt) &&
                ticket.Properties.ExpiresUtc == SessionTicketPolicy.NativeTime(row.IdleExpiresAt) ? ticket : null;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or ArgumentException or IOException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Requires authenticated fields to agree exactly with all queryable metadata; revocation remains a separate denial.</summary>
    private static bool Matches(AdminSessionTicket row, AdminSessionTicket authenticated) =>
        row.Digest == authenticated.Digest && row.Subject == authenticated.Subject && row.SourceSession == authenticated.SourceSession &&
        row.CreatedAt == authenticated.CreatedAt && row.LastActivityAt == authenticated.LastActivityAt &&
        row.AbsoluteExpiresAt == authenticated.AbsoluteExpiresAt && row.IdleLifetime == authenticated.IdleLifetime &&
        row.IdleExpiresAt == authenticated.IdleExpiresAt && row.Revision == authenticated.Revision;

    /// <summary>Bounds the framework representation on both serialization and deserialization paths.</summary>
    private static void RequireNativeSize(byte[] native)
    {
        if (native.Length is 0 or > SessionTicketLimits.NativeBytes)
        {
            throw new ArgumentException(InvalidTicket);
        }
    }

    /// <summary>Defines an authenticated, versioned container without a generated payload-bearing string representation.</summary>
    internal sealed class Envelope
    {
        public int Version
        {
            get; set;
        }
        public AdminSessionTicket? State
        {
            get; set;
        }
        public byte[]? Ticket
        {
            get; set;
        }
    }
}
