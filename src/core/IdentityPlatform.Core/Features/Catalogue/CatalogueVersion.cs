using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace IdentityPlatform.Core.Features.Catalogue;

/// <summary>Creates opaque strong catalogue versions bound to exact resource kind, identifier and positive persisted revision.</summary>
/// <example><code>var etag = CatalogueVersion.Create("scope", scopeId, 1);</code></example>
public static class CatalogueVersion
{
    public const string Scope = "scope";
    public const string Audience = "audience";
    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdef");
    private const int DigestCharacters = 64;
    private const int TagCharacters = DigestCharacters + 2;
    private const string Separator = "\0";
    private const string InvalidKind = "Unsupported catalogue resource kind.";
    private const string InvalidId = "Invalid catalogue resource identifier.";

    /// <summary>Hashes unambiguous kind/id/revision framing; this tag is a concurrency precondition, never an authorization capability.</summary>
    /// <example><code>response.Headers.ETag = CatalogueVersion.Create(CatalogueVersion.Audience, audienceId, revision);</code></example>
    public static string Create(string kind, string id, long revision)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
        if (kind is not Scope and not Audience)
        {
            throw new ArgumentException(InvalidKind, nameof(kind));
        }
        if (!CatalogueRules.IsIdentifier(id))
        {
            throw new ArgumentException(InvalidId, nameof(id));
        }
        var input = string.Join(Separator, kind, id, revision.ToString(CultureInfo.InvariantCulture));
        return string.Concat('"', Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input))), '"');
    }

    /// <summary>Rejects weak/list/wildcard/case/whitespace aliases before exact resource and current revision comparison.</summary>
    /// <example><code>var current = CatalogueVersion.Matches(ifMatch, CatalogueVersion.Scope, scopeId, revision);</code></example>
    public static bool Matches(string? value, string kind, string id, long revision) =>
        IsCanonical(value) && string.Equals(value, Create(kind, id, revision), StringComparison.Ordinal);

    /// <summary>Recognizes only one canonical strong version; syntax validity does not imply a match to current storage.</summary>
    /// <example><code>var valid = CatalogueVersion.IsCanonical(request.Headers.IfMatch.ToString());</code></example>
    public static bool IsCanonical(string? value) => value is { Length: TagCharacters } && value[0] == '"' && value[^1] == '"' &&
        value.AsSpan(1, DigestCharacters).IndexOfAnyExcept(HexDigits) < 0;
}
