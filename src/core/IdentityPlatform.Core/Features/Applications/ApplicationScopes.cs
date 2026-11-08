using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using IdentityPlatform.Core.Features.Catalogue;

namespace IdentityPlatform.Core.Features.Applications;

/// <summary>Exposes a bounded assignment under the application's strong version without credentials or native configuration bags.</summary>
/// <example><code>var assignment = new ApplicationScopes(id, ["email"], authorityExpiry);</code></example>
public sealed record ApplicationScopes(
    [property: JsonRequired, MinLength(1), MaxLength(ApplicationProjection.IdentifierLength)] string ApplicationId,
    [property: JsonRequired, MaxLength(CatalogueRules.MaximumScopes)] IReadOnlyList<string> Scopes,
    [property: JsonRequired] DateTimeOffset ExpiresAt);

/// <summary>Owns the finite assignment subresource and validates safe projections independently of transport annotations.</summary>
/// <example><code>var valid = ApplicationScopeRules.IsValid(assignment, applicationId);</code></example>
public static class ApplicationScopeRules
{
    public const string Suffix = "/scopes";

    /// <summary>Rejects foreign applications, duplicate/unbounded names and noncanonical ordering before publishing upstream state.</summary>
    /// <example><code>if (!ApplicationScopeRules.IsValid(assignment, id)) throw new InvalidDataException();</code></example>
    public static bool IsValid(ApplicationScopes value, string id) => value.ApplicationId == id &&
        ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength) && CatalogueRules.IsAssignment(value.Scopes) &&
        value.Scopes.SequenceEqual(value.Scopes.Order(StringComparer.Ordinal), StringComparer.Ordinal);
}
