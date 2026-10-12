using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Creates bounded opaque locators and browser proofs with constant-time digest comparisons.</summary>
internal static class CeremonySecrets
{
    private const int RandomBytes = 32;
    private const int IdentifierLength = 43;
    private const int DigestLength = 64;

    /// <summary>Creates a 256-bit browser secret or public locator with URL-safe encoding.</summary>
    internal static string Create() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(RandomBytes));

    /// <summary>Rejects unbounded or noncanonical input before a database lookup.</summary>
    internal static bool IsIdentifier(string? value) => value is { Length: IdentifierLength } &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_') &&
        WebEncoders.Base64UrlEncode(WebEncoders.Base64UrlDecode(value)) == value;

    /// <summary>Stores a one-way fixed-width digest instead of a browser correlation secret.</summary>
    internal static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(value)));

    /// <summary>Compares only canonical proofs, without exposing submitted values in diagnostics.</summary>
    internal static bool Matches(string? value, string digest) => IsIdentifier(value) && digest.Length == DigestLength &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Digest(value!)), Encoding.ASCII.GetBytes(digest));
}
