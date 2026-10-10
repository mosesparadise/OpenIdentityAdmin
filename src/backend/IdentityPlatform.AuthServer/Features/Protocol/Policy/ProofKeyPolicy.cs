using System.Buffers;
using System.Buffers.Text;
using IdentityPlatform.AuthServer.Features.Accounts.Login;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.AuthServer.Features.Protocol.Policy;

// Native OpenIddict owns the proof comparison. This profile also enforces RFC 7636 syntax.
public static class ProofKeyPolicy
{
    public static bool IsChallenge(string? value)
    {
        if (!InteractionSecrets.IsIdentifier(value))
        {
            return false;
        }
        Span<byte> decoded = stackalloc byte[32];
        return Base64Url.DecodeFromChars(value, decoded, out var consumed, out var written) == OperationStatus.Done &&
            consumed == 43 && written == 32 &&
            string.Equals(WebEncoders.Base64UrlEncode(decoded), value, StringComparison.Ordinal);
    }

    public static bool IsVerifier(string? value) => value is { Length: >= 43 and <= 128 } &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~');
}
