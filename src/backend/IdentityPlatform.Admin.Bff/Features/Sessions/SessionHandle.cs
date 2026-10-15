using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Admin.Bff.Features.Sessions;

/// <summary>Generates secret references and derives their canonical non-secret database lookup digests.</summary>
internal static class SessionHandle
{
    private const int EntropyBytes = 32;
    private const int EncodedLength = 43;

    /// <summary>Returns a fresh store-owned reference; callers must not persist or log the handle.</summary>
    internal static (string Handle, string Digest) Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(EntropyBytes);
        return (WebEncoders.Base64UrlEncode(bytes), Convert.ToHexString(SHA256.HashData(bytes)));
    }

    /// <summary>Rejects padded, oversized and alternative encodings before hashing or database access.</summary>
    internal static bool TryDigest(string? handle, out string digest)
    {
        digest = string.Empty;
        if (handle?.Length != EncodedLength)
        {
            return false;
        }
        try
        {
            var bytes = WebEncoders.Base64UrlDecode(handle);
            if (bytes.Length != EntropyBytes || WebEncoders.Base64UrlEncode(bytes) != handle)
            {
                return false;
            }
            digest = Convert.ToHexString(SHA256.HashData(bytes));
            return true;
        }
        catch (FormatException) { return false; }
    }
}
