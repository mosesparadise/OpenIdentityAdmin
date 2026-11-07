using System.ComponentModel.DataAnnotations;

namespace IdentityPlatform.Core.Features.Catalogue;

/// <summary>Describes bounded audience metadata; the resource is immutable after creation and never a credential destination.</summary>
/// <example><code>var input = new AudienceInput("orders-api", "Orders", "Operations", true);</code></example>
public sealed record AudienceInput(
    [property: Required, MinLength(1), MaxLength(CatalogueRules.ResourceLength)] string Resource,
    [property: Required, MinLength(1), MaxLength(CatalogueRules.DisplayLength)] string DisplayName,
    [property: Required, MinLength(1), MaxLength(CatalogueRules.DisplayLength)] string Owner,
    bool Enabled);

/// <summary>Describes one finite identity or API scope policy without accepting arbitrary native properties or claim expressions.</summary>
/// <example><code>var input = new ScopeInput("email", "Email", "identity", true, "Share email", null, "email");</code></example>
public sealed record ScopeInput(
    [property: Required, MinLength(1), MaxLength(CatalogueRules.NameLength)] string Name,
    [property: Required, MinLength(1), MaxLength(CatalogueRules.DisplayLength)] string DisplayName,
    [property: Required] string Kind,
    bool Enabled,
    [property: Required, MinLength(1), MaxLength(CatalogueRules.ConsentLength)] string ConsentText,
    [property: MaxLength(CatalogueRules.IdentifierLength)] string? AudienceId,
    [property: Required] string Disclosure);

/// <summary>Requests the full explicit additional scope set; empty removes assignments while implicit OpenID behavior remains.</summary>
/// <example><code>var assignment = new ApplicationScopeInput(new[] { "email", "orders.read" });</code></example>
public sealed record ApplicationScopeInput([property: Required, MaxLength(CatalogueRules.MaximumScopes)] IReadOnlyList<string> Scopes);
