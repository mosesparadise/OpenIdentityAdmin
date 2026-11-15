using System.ComponentModel.DataAnnotations;

namespace IdentityPlatform.Core.Features.Applications;

/// <summary>Confirms copying a versioned managed configuration into a new disabled identity with a newly issued credential.</summary>
/// <example><code>var clone = new ApplicationClone(true, "example-copy", "Example copy", "2030-01-01T00:00:00Z"); // Submit with the source ETag.</code></example>
public sealed record ApplicationClone(
    bool Confirmed,
    [property: Required, MinLength(1), MaxLength(ApplicationProjection.ClientIdentifierLength)] string ClientId,
    [property: Required, MinLength(1), MaxLength(ApplicationProjection.DisplayNameLength)] string DisplayName,
    [property: Required] string CredentialExpiresAt);

/// <summary>Defines only clone input constraints; current source and catalogue authority must be established inside the owned transaction.</summary>
/// <example><code>if (!ApplicationCloneRules.IsValid(input, clock.GetUtcNow())) return Results.BadRequest();</code></example>
public static class ApplicationCloneRules
{
    public const string Suffix = "/clone";

    /// <summary>Rejects implicit confirmation, malformed identities/names and nonfuture or noncanonical expiry without normalizing intent.</summary>
    /// <example><code>var valid = ApplicationCloneRules.IsValid(input, DateTimeOffset.UtcNow);</code></example>
    public static bool IsValid(ApplicationClone input, DateTimeOffset now) => input.Confirmed &&
        ApplicationEditorRules.IsClientId(input.ClientId) &&
        ApplicationEditorRules.IsText(input.DisplayName, ApplicationProjection.DisplayNameLength) &&
        ApplicationEditorRules.TryReadExpiry(input.CredentialExpiresAt, now, out _);
}
