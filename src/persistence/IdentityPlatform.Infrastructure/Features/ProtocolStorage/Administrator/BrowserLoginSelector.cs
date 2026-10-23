using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Shares canonical 256-bit selector representation between native transport and durable issuer state.</summary>
internal static class BrowserLoginSelector
{
    private const int SelectorBytes = 32;
    private const int SelectorCharacters = 43;

    /// <summary>Returns a canonical digest only for one exact unpadded 256-bit selector, rejecting strict decoder failures.</summary>
    internal static string? Digest(string? value)
    {
        if (value is not { Length: SelectorCharacters } ||
            !value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
        {
            return null;
        }
        try
        {
            var bytes = WebEncoders.Base64UrlDecode(value);
            return bytes.Length == SelectorBytes && WebEncoders.Base64UrlEncode(bytes) == value
                ? Convert.ToHexString(SHA256.HashData(bytes)) : null;
        }
        catch (FormatException) { return null; }
    }

    /// <summary>Creates a separate unpredictable completion proof; persistence stores only its canonical digest.</summary>
    internal static string CreateSelector() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(SelectorBytes));

}
