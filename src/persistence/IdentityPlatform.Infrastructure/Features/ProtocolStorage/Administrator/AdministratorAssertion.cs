using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Retains bounded assertion inputs for serialized consumption; creation alone is not cryptographic acceptance or replay authority.</summary>
internal sealed record AdministratorAssertion
{
    internal const int LifetimeSeconds = ClientAssertionLimits.LifetimeSeconds;
    internal const int ClockSkewSeconds = ClientAssertionLimits.ClockSkewSeconds;
    private const int LengthBytes = sizeof(int);
    internal string Digest
    {
        get;
    }
    internal long IssuedAt
    {
        get;
    }
    internal long ExpiresAt
    {
        get;
    }
    internal long? NotBefore
    {
        get;
    }

    /// <summary>Stores only a domain-bound digest, discarding the raw identifier after validated bounded construction.</summary>
    private AdministratorAssertion(string identifier, long issuedAt, long expiresAt, long? notBefore)
    {
        Digest = Hash(identifier);
        IssuedAt = issuedAt;
        ExpiresAt = expiresAt;
        NotBefore = notBefore;
    }

    /// <summary>Rejects malformed identifiers and unbounded/overflowing lifetime inputs before persistence can retain them.</summary>
    internal static AdministratorAssertion? Create(string identifier, long issuedAt, long expiresAt, long? notBefore) =>
        ClientAssertionLimits.Valid(identifier, issuedAt, expiresAt, notBefore) ? new(identifier, issuedAt, expiresAt, notBefore) : null;

    /// <summary>Checks this assertion against the serialized protocol observation and the fixed skew policy.</summary>
    internal bool Allows(long effectiveNow) => ClientAssertionLimits.Allows(IssuedAt, ExpiresAt, NotBefore, effectiveNow);

    /// <summary>Hashes separate big-endian UTF-8 lengths and values so the shared PAR/token ledger has no concatenation ambiguity.</summary>
    private static string Hash(string identifier)
    {
        var client = Encoding.UTF8.GetBytes(AdministratorProtocolIdentity.ClientId);
        var id = Encoding.UTF8.GetBytes(identifier);
        var bytes = new byte[LengthBytes + client.Length + LengthBytes + id.Length];
        BinaryPrimitives.WriteInt32BigEndian(bytes, client.Length);
        client.CopyTo(bytes, LengthBytes);
        BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(LengthBytes + client.Length), id.Length);
        id.CopyTo(bytes, LengthBytes + client.Length + LengthBytes);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
