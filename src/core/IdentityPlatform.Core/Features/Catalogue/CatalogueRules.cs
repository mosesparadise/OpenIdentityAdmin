using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Core.Features.Catalogue;

/// <summary>Defines finite catalogue identifiers and policy combinations before native persistence or protocol admission.</summary>
/// <example><code>var errors = CatalogueRules.Validate(new AudienceInput("orders-api", "Orders", "Operations", true));</code></example>
public static class CatalogueRules
{
    public const int NameLength = 100;
    public const int ResourceLength = 200;
    public const int IdentifierLength = 100;
    public const int DisplayLength = 200;
    public const int ConsentLength = 500;
    public const int MaximumScopes = 32;
    public const int MaximumRequestBytes = 32_768;
    public const int MaximumDecisionBytes = 16_384;
    public const string Identity = "identity";
    public const string Api = "api";
    public const string None = "none";
    public const string Email = "email";
    private const string ResourceField = "resource";
    private const string DisplayNameField = "displayName";
    private const string OwnerField = "owner";
    private const string NameField = "name";
    private const string ConsentField = "consentText";
    private const string KindField = "kind";
    private const string AudienceField = "audienceId";
    private const string DisclosureField = "disclosure";
    private const string SystemPrefix = "identityplatform";
    private const string Invalid = "The value is missing, unsupported or outside its allowed bounds.";
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
        { "openid", "offline_access", "profile", "phone", "address", "identity.admin", "identity-admin-api", "identity-admin-bff", "pilot_api" };

    /// <summary>Requires exact ASCII protocol spelling and rejects fixed/system scope aliases without lowercasing user identifiers.</summary>
    /// <example><code>var valid = CatalogueRules.IsName("orders.read");</code></example>
    public static bool IsName(string? value) => Protocol(value, NameLength, false) &&
        !Reserved.Contains(value!) && !value!.StartsWith(SystemPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Admits bounded resource identities without interpreting them as a network destination or OAuth client.</summary>
    /// <example><code>var valid = CatalogueRules.IsResource("https://api.example/orders");</code></example>
    public static bool IsResource(string? value) => Protocol(value, ResourceLength, true) &&
        !Reserved.Contains(value!) && !value!.StartsWith(SystemPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Accepts exact opaque native identifiers without whitespace, invalid Unicode or control aliases.</summary>
    /// <example><code>var valid = CatalogueRules.IsIdentifier(nativeScopeId);</code></example>
    public static bool IsIdentifier(string? value) => ApplicationEditorRules.IsText(value, IdentifierLength) && !value!.Any(char.IsWhiteSpace);

    /// <summary>Returns value-free errors for the complete audience intent; immutable identity is separately compared with persisted state.</summary>
    /// <example><code>var errors = CatalogueRules.Validate(new AudienceInput("orders-api", "Orders", "Operations", true));</code></example>
    public static IReadOnlyDictionary<string, string[]> Validate(AudienceInput input)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        Check(errors, ResourceField, IsResource(input.Resource));
        Check(errors, DisplayNameField, ApplicationEditorRules.IsText(input.DisplayName, DisplayLength));
        Check(errors, OwnerField, ApplicationEditorRules.IsText(input.Owner, DisplayLength));
        return errors;
    }

    /// <summary>Rejects identity resources, API identity disclosure and unknown policy values instead of inferring a permissive default.</summary>
    /// <example><code>var errors = CatalogueRules.Validate(new ScopeInput("email", "Email", "identity", true, "Share email", null, "email"));</code></example>
    public static IReadOnlyDictionary<string, string[]> Validate(ScopeInput input)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        Check(errors, NameField, IsName(input.Name));
        Check(errors, DisplayNameField, ApplicationEditorRules.IsText(input.DisplayName, DisplayLength));
        Check(errors, ConsentField, ApplicationEditorRules.IsText(input.ConsentText, ConsentLength));
        Check(errors, KindField, input.Kind is Identity or Api);
        Check(errors, AudienceField, input.Kind == Identity ? input.AudienceId is null : input.Kind == Api && IsIdentifier(input.AudienceId));
        Check(errors, DisclosureField, input.Disclosure is None or Email && (input.Kind == Identity || input.Disclosure == None) &&
            (!string.Equals(input.Name, Email, StringComparison.OrdinalIgnoreCase) || input.Name == Email && input.Kind == Identity && input.Disclosure == Email));
        return errors;
    }

    /// <summary>Allows explicit removal but rejects duplicates, reserved names and oversized assignment arrays before any native write.</summary>
    /// <example><code>var valid = CatalogueRules.IsAssignment(new[] { "email", "orders.read" });</code></example>
    public static bool IsAssignment(IReadOnlyList<string>? names) => names is not null && names.Count <= MaximumScopes &&
        names.All(IsName) && names.Distinct(StringComparer.Ordinal).Count() == names.Count;

    /// <summary>Checks the deliberately small ASCII grammar without trimming, case folding or accepting URI/parser aliases.</summary>
    private static bool Protocol(string? value, int maximum, bool slash) => value is { Length: > 0 } && value.Length <= maximum &&
        char.IsAsciiLetterOrDigit(value[0]) && value.All(character => char.IsAsciiLetterOrDigit(character) ||
            character is '.' or '_' or ':' or '-' || slash && character == '/');

    /// <summary>Produces fixed field/message pairs without echoing untrusted values into errors or telemetry.</summary>
    private static void Check(Dictionary<string, string[]> errors, string field, bool valid)
    {
        if (!valid)
        {
            errors.Add(field, [Invalid]);
        }
    }
}
