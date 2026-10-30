using System.Globalization;
using System.Text;

namespace IdentityPlatform.Core.Features.Applications;

/// <summary>Defines finite cross-provider values for the selected confidential-web registration contract.</summary>
/// <example><code>var errors = ApplicationEditorRules.Validate(new ApplicationEdit("Example", ["https://client.example/callback"]));</code></example>
public static class ApplicationEditorRules
{
    public const string Profile = "managed-web-v1";
    public const int MaximumRedirects = 10;
    public const int MaximumRedirectLength = 2048;
    public const int MaximumRequestBytes = 32_768;
    public const string ExpiryFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const string ClientIdField = "clientId";
    private const string DisplayNameField = "displayName";
    private const string RedirectsField = "redirectUris";
    private const string ExpiryField = "credentialExpiresAt";
    private const string ConfirmationField = "confirmed";
    private const string InvalidValue = "The value is missing, unsupported or outside its allowed bounds.";

    /// <summary>Validates creation without trimming identifiers, generating credentials or reading wall time implicitly.</summary>
    /// <example><code>var errors = ApplicationEditorRules.Validate(input, timeProvider.GetUtcNow());</code></example>
    public static IReadOnlyDictionary<string, string[]> Validate(ApplicationCreate input, DateTimeOffset now)
    {
        var errors = ValidateFields(input.DisplayName, input.RedirectUris);
        if (!IsClientId(input.ClientId))
        {
            errors.Add(ClientIdField, [InvalidValue]);
        }
        if (!TryReadExpiry(input.CredentialExpiresAt, now, out _))
        {
            errors.Add(ExpiryField, [InvalidValue]);
        }
        return errors;
    }

    /// <summary>Validates only mutable noncredential fields; callers separately require current authority and version.</summary>
    /// <example><code>var errors = ApplicationEditorRules.Validate(new ApplicationEdit("Example", ["https://client.example/callback"]));</code></example>
    public static IReadOnlyDictionary<string, string[]> Validate(ApplicationEdit input) => ValidateFields(input.DisplayName, input.RedirectUris);

    /// <summary>Requires explicit confirmation and a strictly future exact UTC second; neither grants replacement authority.</summary>
    /// <example><code>var errors = ApplicationEditorRules.Validate(replacement, timeProvider.GetUtcNow());</code></example>
    public static IReadOnlyDictionary<string, string[]> Validate(ApplicationSecretReplacement input, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (!input.Confirmed)
        {
            errors.Add(ConfirmationField, [InvalidValue]);
        }
        if (!TryReadExpiry(input.CredentialExpiresAt, now, out _))
        {
            errors.Add(ExpiryField, [InvalidValue]);
        }
        return errors;
    }

    /// <summary>Reads the single wire representation and rejects equality, offsets, fractions and parser aliases.</summary>
    /// <example><code>var valid = ApplicationEditorRules.TryReadExpiry("2030-01-01T00:00:00Z", clock.GetUtcNow(), out var expiry);</code></example>
    public static bool TryReadExpiry(string? value, DateTimeOffset now, out DateTimeOffset expiry) =>
        DateTimeOffset.TryParseExact(value, ExpiryFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out expiry) &&
        value == expiry.ToString(ExpiryFormat, CultureInfo.InvariantCulture) && expiry > now;

    /// <summary>Retains exact case and Unicode identity while excluding padding aliases, controls and invalid UTF-16.</summary>
    /// <example><code>var valid = ApplicationEditorRules.IsClientId("example-web");</code></example>
    public static bool IsClientId(string? value) => IsText(value, ApplicationProjection.ClientIdentifierLength) &&
        !value!.Any(char.IsWhiteSpace);

    /// <summary>Rejects invalid Unicode before JSON serialization or either provider can replace or reinterpret it.</summary>
    /// <example><code>var valid = ApplicationEditorRules.IsText("Example", 200);</code></example>
    public static bool IsText(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value.Any(char.IsControl))
        {
            return false;
        }
        for (var index = 0; index < value.Length;)
        {
            if (!Rune.TryGetRuneAt(value, index, out var rune))
            {
                return false;
            }
            index += rune.Utf16SequenceLength;
        }
        return true;
    }

    /// <summary>Accepts only already canonical absolute HTTPS callbacks with exact matching and no userinfo or fragment.</summary>
    /// <example><code>var valid = ApplicationEditorRules.IsRedirect("https://client.example/callback");</code></example>
    public static bool IsRedirect(string? value) => IsText(value, MaximumRedirectLength) && !value!.Any(char.IsWhiteSpace) &&
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsWellFormedOriginalString() && uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment) && uri.AbsoluteUri == value;

    /// <summary>Returns only fixed field identifiers and value-free messages, preserving exact callback multiplicity.</summary>
    private static Dictionary<string, string[]> ValidateFields(string? displayName, IReadOnlyList<string>? redirects)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (!IsText(displayName, ApplicationProjection.DisplayNameLength))
        {
            errors.Add(DisplayNameField, [InvalidValue]);
        }
        if (redirects is null || redirects.Count is < 1 or > MaximumRedirects || redirects.Any(value => !IsRedirect(value)) ||
            redirects.Distinct(StringComparer.Ordinal).Count() != redirects.Count)
        {
            errors.Add(RedirectsField, [InvalidValue]);
        }
        return errors;
    }
}
