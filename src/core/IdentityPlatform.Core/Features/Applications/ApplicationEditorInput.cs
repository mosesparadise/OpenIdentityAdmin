using System.ComponentModel.DataAnnotations;

namespace IdentityPlatform.Core.Features.Applications;

/// <summary>Requests the one supported managed web profile without accepting arbitrary native permissions or credentials.</summary>
/// <example><code>var input = new ApplicationCreate("example-web", "Example", ["https://client.example/callback"], "2030-01-01T00:00:00Z");</code></example>
public sealed record ApplicationCreate(
    [property: Required, MinLength(1), MaxLength(ApplicationProjection.ClientIdentifierLength)] string ClientId,
    [property: Required, MinLength(1), MaxLength(ApplicationProjection.DisplayNameLength)] string DisplayName,
    [property: Required, MinLength(1), MaxLength(ApplicationEditorRules.MaximumRedirects)] IReadOnlyList<string> RedirectUris,
    [property: Required] string CredentialExpiresAt);

/// <summary>Updates only presentation and exact callbacks; credential lifecycle and client identity are deliberately absent.</summary>
/// <example><code>var edit = new ApplicationEdit("Updated example", ["https://client.example/new-callback"]);</code></example>
public sealed record ApplicationEdit(
    [property: Required, MinLength(1), MaxLength(ApplicationProjection.DisplayNameLength)] string DisplayName,
    [property: Required, MinLength(1), MaxLength(ApplicationEditorRules.MaximumRedirects)] IReadOnlyList<string> RedirectUris);

/// <summary>Requests explicitly confirmed secret replacement with an operator-selected expiry and no plaintext input.</summary>
/// <example><code>var replacement = new ApplicationSecretReplacement(true, "2030-01-01T00:00:00Z");</code></example>
public sealed record ApplicationSecretReplacement(bool Confirmed, [property: Required] string CredentialExpiresAt);
