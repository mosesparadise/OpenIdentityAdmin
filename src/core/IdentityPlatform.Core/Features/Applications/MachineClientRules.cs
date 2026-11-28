using System.ComponentModel.DataAnnotations;
using System.Text;
using IdentityPlatform.Core.Features.Catalogue;

namespace IdentityPlatform.Core.Features.Applications;

/// <summary>Registers a confidential service with an explicit credential lifetime and no interactive callback.</summary>
/// <example><code>var service = new ApplicationMachineCreate("orders-indexer", "Order indexing", "2030-01-01T00:00:00Z");</code></example>
public sealed record ApplicationMachineCreate(
    [property: Required, MinLength(1), MaxLength(ApplicationProjection.ClientIdentifierLength)] string ClientId,
    [property: Required, MinLength(1), MaxLength(ApplicationProjection.DisplayNameLength)] string DisplayName,
    [property: Required] string CredentialExpiresAt);

/// <summary>Defines the finite machine-client profile, bounded scope request and stable non-human subject.</summary>
/// <example><code>var valid = MachineClientRules.IsValid(service, clock.GetUtcNow());</code></example>
public static class MachineClientRules
{
    public const string Profile = "managed-machine-v1";
    public const string KeyRegistrationSuffix = "/machine-key-registrations";
    public const string KeyReplacementSuffix = "/key-replacements";
    public const string KeyProfile = "managed-machine-key-v1";
    public const string Suffix = "/machine-registrations";
    public const string IdentityType = "identity_type";
    public const string Machine = "machine";
    private const string SubjectPrefix = "machine:";
    private const int MaximumScopeLength = CatalogueRules.MaximumScopes * (CatalogueRules.NameLength + 1);

    /// <summary>Validates an exact identifier, display name and future canonical credential expiry.</summary>
    /// <example><code>var valid = MachineClientRules.IsValid(request, clock.GetUtcNow());</code></example>
    public static bool IsValid(ApplicationMachineCreate input, DateTimeOffset now) =>
        ApplicationEditorRules.IsClientId(input.ClientId) && ApplicationEditorRules.IsText(input.DisplayName, ApplicationProjection.DisplayNameLength) &&
        ApplicationEditorRules.TryReadExpiry(input.CredentialExpiresAt, now, out _);

    /// <summary>Changes only the name of a service registration, which has no interactive callbacks.</summary>
    /// <example><code>var valid = MachineClientRules.IsEdit(new ApplicationEdit("Order indexing", []));</code></example>
    public static bool IsEdit(ApplicationEdit input) => input.RedirectUris is { Count: 0 } &&
        ApplicationEditorRules.IsText(input.DisplayName, ApplicationProjection.DisplayNameLength);

    /// <summary>Reads an explicit API scope set before catalogue resolution. Machine clients have no identity or administrator scopes.</summary>
    /// <example><code>var names = MachineClientRules.ReadScopes("orders.read");</code></example>
    public static string[]? ReadScopes(string? value)
    {
        if (value is not { Length: > 0 and <= MaximumScopeLength })
        {
            return null;
        }
        var names = value.Split(' ');
        return CatalogueRules.IsAssignment(names) && !names.Contains(CatalogueRules.Email, StringComparer.OrdinalIgnoreCase)
            ? names.Order(StringComparer.Ordinal).ToArray() : null;
    }

    /// <summary>Builds a stable subject from the immutable native registration ID in a separate namespace from human Identity IDs.</summary>
    /// <example><code>var subject = MachineClientRules.Subject(permanentApplicationId);</code></example>
    public static string Subject(string applicationId) => SubjectPrefix + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(applicationId)));
}
