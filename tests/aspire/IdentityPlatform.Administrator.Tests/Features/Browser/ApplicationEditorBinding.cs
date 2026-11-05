using System.Security.Cryptography;
using System.Text;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Binds genuine UI requests and visible credential material without reading the browser inspector's response-body cache.</summary>
internal static class ApplicationEditorBinding
{
    private const string Collection = "/bff/api/v1/applications/";
    private const string Json = "application/json";
    private const string NoStore = "no-store";
    private const string NoSniff = "nosniff";
    private const string ContentType = "content-type";
    private const string CacheControl = "cache-control";
    private const string ContentOptions = "x-content-type-options";
    private const string SetCookie = "set-cookie";
    private const string ETag = "etag";
    private const int SecretBytes = 32;
    private const int SecretCharacters = 44;
    private const int IdentifierCharacters = 400;
    private const long CreatedRevision = 1;
    private const long EditedRevision = 2;

    /// <summary>Requires the real mutation's security headers and returns its opaque version without retaining private response contents.</summary>
    internal static async Task<string> HeadersAsync(IResponse response)
    {
        Assert.True((await response.HeaderValueAsync(ContentType))?.Split(';')[0].Trim() == Json);
        Assert.True((await response.HeaderValueAsync(CacheControl))?.Split(',').Any(value => value.Trim() == NoStore) == true);
        Assert.True(await response.HeaderValueAsync(ContentOptions) == NoSniff);
        Assert.Null(await response.HeaderValueAsync(SetCookie));
        var version = await response.HeaderValueAsync(ETag);
        Assert.True(!string.IsNullOrEmpty(version));
        return version!;
    }

    /// <summary>Obtains the opaque ID from the already-required UI edit and binds both versions to the exact original creation response.</summary>
    internal static string CreatedIdentity(Uri origin, string target, string createdVersion, string? precondition, string editedVersion)
    {
        Assert.True(Uri.TryCreate(target, UriKind.Absolute, out var uri));
        Assert.True(uri!.GetLeftPart(UriPartial.Authority) == origin.GetLeftPart(UriPartial.Authority) &&
            uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.AbsolutePath.StartsWith(Collection, StringComparison.Ordinal));
        var id = Uri.UnescapeDataString(uri.AbsolutePath[Collection.Length..]);
        Assert.True(id.Length is > 0 and <= IdentifierCharacters && !id.Any(char.IsControl));
        Assert.True(target == new Uri(origin, Collection + Uri.EscapeDataString(id)).AbsoluteUri);
        Assert.True(createdVersion == Version(id, CreatedRevision) && precondition == createdVersion && editedVersion == Version(id, EditedRevision));
        return id;
    }

    /// <summary>Independently reproduces the documented exact UTF-8 resource digest and positive aggregate revision.</summary>
    internal static string Version(string id, long revision) =>
        string.Concat('"', Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(id))), '-',
            revision.ToString(System.Globalization.CultureInfo.InvariantCulture), '"');

    /// <summary>Requires the visible native 32-byte credential's canonical Base64 encoding without exposing its value in assertion messages.</summary>
    internal static void Secret(string value)
    {
        Span<byte> bytes = stackalloc byte[SecretBytes];
        try
        {
            Assert.True(value.Length == SecretCharacters && Convert.TryFromBase64String(value, bytes, out var length) &&
                length == SecretBytes && Convert.ToBase64String(bytes) == value);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
