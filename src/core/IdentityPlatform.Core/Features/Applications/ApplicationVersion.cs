using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace IdentityPlatform.Core.Features.Applications;

/// <summary>Binds one strong precondition to an exact opaque application identifier and positive aggregate revision.</summary>
/// <example><code>var etag = ApplicationVersion.Create(applicationId, 1);</code></example>
public static class ApplicationVersion
{
    private const string InvalidIdentifier = "Application identifier is outside its supported bounds.";
    private const string Separator = "-";
    private const int RevisionStart = 66;
    private const int TagPunctuationCharacters = 3;
    private const int DigestCharacters = 64;
    private const int MinimumTagLength = DigestCharacters + 4;
    private const int MaximumRevisionCharacters = 19;
    private const int MaximumTagLength = DigestCharacters + MaximumRevisionCharacters + 3;

    /// <summary>Creates the canonical quoted strong tag; identifiers are hashed as exact UTF-8 with no normalization.</summary>
    /// <example><code>response.Headers.ETag = ApplicationVersion.Create(applicationId, revision);</code></example>
    public static string Create(string applicationId, long revision)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        if (!ApplicationEditorRules.IsText(applicationId, ApplicationProjection.IdentifierLength))
        {
            throw new ArgumentException(InvalidIdentifier, nameof(applicationId));
        }
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(applicationId)));
        return string.Concat('"', digest, Separator, revision.ToString(CultureInfo.InvariantCulture), '"');
    }

    /// <summary>Rejects weak, list, wildcard, whitespace, case and numeric aliases before comparing the exact resource binding.</summary>
    /// <example><code>var current = ApplicationVersion.Matches(request.Headers.IfMatch.ToString(), applicationId, revision);</code></example>
    public static bool Matches(string? value, string applicationId, long revision) =>
        IsCanonical(value) && string.Equals(value, Create(applicationId, revision), StringComparison.Ordinal);

    /// <summary>Checks one strong-tag wire grammar without accepting a tag as authorization or a substitute for current storage.</summary>
    /// <example><code>if (!ApplicationVersion.IsCanonical(ifMatch)) return Results.StatusCode(412);</code></example>
    public static bool IsCanonical(string? value)
    {
        if (value is null || value.Length is < MinimumTagLength or > MaximumTagLength || value[0] != '"' || value[^1] != '"' ||
            value[DigestCharacters + 1] != '-')
        {
            return false;
        }
        var digest = value.AsSpan(1, DigestCharacters);
        foreach (var character in digest)
        {
            if (!char.IsAsciiHexDigitLower(character))
            {
                return false;
            }
        }
        var number = value.AsSpan(RevisionStart, value.Length - DigestCharacters - TagPunctuationCharacters);
        return long.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var revision) && revision > 0 &&
            number.SequenceEqual(revision.ToString(CultureInfo.InvariantCulture));
    }
}
