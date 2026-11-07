namespace IdentityPlatform.Core.Features.Catalogue;

/// <summary>Exposes safe audience metadata and its current resource-bound version, never native credentials or internal approval state.</summary>
/// <example><code>var details = new AudienceDetails(id, "orders-api", "Orders", "Operations", true, version, false);</code></example>
public sealed record AudienceDetails(string Id, string Resource, string DisplayName, string Owner, bool Enabled, string Version, bool Referenced);

/// <summary>Exposes native scope identity and explicit finite policy without duplicating resource authority in a writable property bag.</summary>
/// <example><code>var details = new ScopeDetails(id, "email", "Email", "identity", true, "Share email", null, null, "email", version, false);</code></example>
public sealed record ScopeDetails(string Id, string Name, string DisplayName, string Kind, bool Enabled, string ConsentText,
    string? AudienceId, string? Resource, string Disclosure, string Version, bool Referenced);

/// <summary>Shows only explicitly assigned additional scope names and the application aggregate's current precondition.</summary>
/// <example><code>var details = new ApplicationScopeDetails(applicationId, new[] { "email" }, applicationVersion);</code></example>
public sealed record ApplicationScopeDetails(string ApplicationId, IReadOnlyList<string> Scopes, string Version);
