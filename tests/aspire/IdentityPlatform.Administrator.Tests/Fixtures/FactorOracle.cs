using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Independent RFC 6238 and length-prefix verifier oracles using published synthetic test material.</summary>
internal static class FactorOracle
{
    private const int StepSeconds = 30;
    private const int CounterBytes = 8;
    private const int OffsetMask = 15;
    private const uint SignMask = 0x7fffffff;
    private const uint Modulus = 1000000;
    private const string Digits = "D6";
    private const string PublishedSeed = "12345678901234567890";

    /// <summary>Computes the published SHA-1 TOTP without calling production factor code or Identity's provider.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5350", Justification = "Independent RFC 6238 SHA-1 interoperability oracle for the native Identity authenticator; not password or signing cryptography.")]
    internal static string Code(long unixSeconds)
    {
        Span<byte> counter = stackalloc byte[CounterBytes];
        BinaryPrimitives.WriteInt64BigEndian(counter, unixSeconds / StepSeconds);
        var digest = HMACSHA1.HashData(Encoding.ASCII.GetBytes(PublishedSeed), counter);
        var offset = digest[^1] & OffsetMask;
        return ((BinaryPrimitives.ReadUInt32BigEndian(digest.AsSpan(offset)) & SignMask) % Modulus).ToString(Digits, CultureInfo.InvariantCulture);
    }

    /// <summary>Builds the documented wire representation independently of the production incremental hash implementation.</summary>
    internal static string Verifier(string userId, long generation, string code)
    {
        const string Domain = "IdentityPlatform.AdministratorRecovery.v1";
        const int PrefixBytes = 4;
        using var stream = new MemoryStream();
        foreach (var segment in new[] { Domain, userId, generation.ToString(CultureInfo.InvariantCulture), code })
        {
            var bytes = Encoding.UTF8.GetBytes(segment);
            var prefix = new byte[PrefixBytes];
            BinaryPrimitives.WriteInt32BigEndian(prefix, bytes.Length);
            stream.Write(prefix);
            stream.Write(bytes);
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
