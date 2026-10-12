using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace IdentityPlatform.Infrastructure.Features.Identity.Factors;

/// <summary>Derives domain-separated verifiers from high-entropy native-manager recovery codes.</summary>
internal static class RecoveryVerifier
{
    internal const int Count = 10;
    internal const int RandomBytes = 16;
    private const int CanonicalLength = 32;
    private const int LengthBytes = 4;
    private const string Domain = "IdentityPlatform.AdministratorRecovery.v1";

    /// <summary>Accepts exactly the canonical ASCII representation, leaving optional display grouping to the HTTP boundary.</summary>
    internal static bool IsCanonical(string code) => code.Length == CanonicalLength && code.All(character =>
        character is >= '0' and <= '9' or >= 'A' and <= 'F');

    /// <summary>Hashes unambiguous UTF-8 length-prefixed segments; stores no reversible code collection.</summary>
    internal static string Hash(string userId, long generation, string code)
    {
        if (string.IsNullOrEmpty(userId) || generation <= 0 || !IsCanonical(code))
        {
            throw new FactorUnavailableException();
        }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[LengthBytes];
        foreach (var part in new[] { Domain, userId, generation.ToString(CultureInfo.InvariantCulture), code })
        {
            var bytes = Encoding.UTF8.GetBytes(part);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
            CryptographicOperations.ZeroMemory(bytes);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
