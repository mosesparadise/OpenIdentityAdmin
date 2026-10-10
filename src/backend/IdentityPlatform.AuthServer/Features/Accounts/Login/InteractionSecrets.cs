using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.AuthServer.Features.Accounts.Login;

public static class InteractionSecrets
{
    public static string Create() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static bool IsIdentifier(string? value) => value is { Length: 43 } &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    public static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static bool Matches(string? value, string expectedDigest) => IsIdentifier(value) &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Digest(value!)), Encoding.ASCII.GetBytes(expectedDigest));
}
