using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace IdentityPlatform.Core.Features.Applications;

/// <summary>Requests the one supported managed web profile without accepting arbitrary native permissions or credentials.</summary>
/// <example><code>var input = new ApplicationCreate("example-web", "Example", ["https://client.example/callback"], "2030-01-01T00:00:00Z");</code></example>
public sealed record ApplicationCreate(
    [property: Required, MinLength(1), MaxLength(ApplicationProjection.ClientIdentifierLength)] string ClientId,
    [property: Required, MinLength(1), MaxLength(ApplicationProjection.DisplayNameLength)] string DisplayName,
    [property: Required, MinLength(1), MaxLength(ApplicationEditorRules.MaximumRedirects)] IReadOnlyList<string> RedirectUris,
    [property: Required] string CredentialExpiresAt);

/// <summary>Registers a native public client with administrator-approved app-linked HTTPS redirects.</summary>
/// <example><code>var input = new ApplicationNativeCreate("mobile", "Mobile", ["https://mobile.example/signin"]);</code></example>
public sealed record ApplicationNativeCreate(
    [property: Required, MinLength(1), MaxLength(ApplicationProjection.ClientIdentifierLength)] string ClientId,
    [property: Required, MinLength(1), MaxLength(ApplicationProjection.DisplayNameLength)] string DisplayName,
    [property: Required, MinLength(1), MaxLength(ApplicationEditorRules.MaximumRedirects)] IReadOnlyList<string> RedirectUris);

/// <summary>Registers a credential-free browser client with separate callback and cross-origin permissions.</summary>
/// <example><code>var input = new ApplicationBrowserCreate("portal", "Portal", ["https://signin.example/callback"], ["https://portal.example"]);</code></example>
public sealed record ApplicationBrowserCreate(
    [property: Required, MinLength(1), MaxLength(ApplicationProjection.ClientIdentifierLength)] string ClientId,
    [property: Required, MinLength(1), MaxLength(ApplicationProjection.DisplayNameLength)] string DisplayName,
    [property: Required, MinLength(1), MaxLength(ApplicationEditorRules.MaximumRedirects)] IReadOnlyList<string> RedirectUris,
    [property: Required, MinLength(1), MaxLength(BrowserOriginPolicy.MaximumOrigins)] IReadOnlyList<string> Origins);

/// <summary>Registers a public client that signs in through the issuer's device verification page.</summary>
/// <example><code>var input = new ApplicationDeviceCreate("meeting-display", "Meeting room display");</code></example>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ApplicationDeviceCreate(
    [property: Required, MinLength(1), MaxLength(ApplicationProjection.ClientIdentifierLength)] string ClientId,
    [property: Required, MinLength(1), MaxLength(ApplicationProjection.DisplayNameLength)] string DisplayName);

/// <summary>Updates only presentation and exact callbacks; credential lifecycle and client identity are deliberately absent.</summary>
/// <example><code>var edit = new ApplicationEdit("Updated example", ["https://client.example/new-callback"]);</code></example>
public sealed record ApplicationEdit(
    [property: Required, MinLength(1), MaxLength(ApplicationProjection.DisplayNameLength)] string DisplayName,
    [property: Required, MaxLength(ApplicationEditorRules.MaximumRedirects)] IReadOnlyList<string> RedirectUris);

/// <summary>Requests explicitly confirmed secret replacement with an operator-selected expiry and no plaintext input.</summary>
/// <example><code>var replacement = new ApplicationSecretReplacement(true, "2030-01-01T00:00:00Z");</code></example>
public sealed record ApplicationSecretReplacement(bool Confirmed, [property: Required] string CredentialExpiresAt);
