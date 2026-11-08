namespace IdentityPlatform.Core.Features.Catalogue;

/// <summary>Defines fixed catalogue routes shared by independent API and browser contracts, never arbitrary forwarding destinations.</summary>
/// <example><code>var path = CataloguePaths.ApiRoot + CataloguePaths.Detail(CatalogueVersion.Scope, scopeId);</code></example>
public static class CataloguePaths
{
    public const string ApiRoot = "/api/v1";
    public const string BrowserRoot = "/bff/api/v1";
    public const string Scopes = "/scopes";
    public const string Audiences = "/audiences";
    public const string Deletions = "/deletions";
    public const string Antiforgery = "/antiforgery";
    private const string Separator = "/";
    private const string Current = ".";
    private const string Parent = "..";
    private const string InvalidId = "Invalid catalogue route identifier.";
    private const string GuidFormat = "D";

    /// <summary>Maps only the two explicit catalogue namespaces; unknown kinds never fall through to another resource.</summary>
    /// <example><code>var collection = CataloguePaths.Collection(CatalogueVersion.Audience);</code></example>
    public static string Collection(string kind) => kind switch
    {
        CatalogueVersion.Scope => Scopes,
        CatalogueVersion.Audience => Audiences,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    /// <summary>Rejects navigation aliases and reserved route segments before percent-encoding one exact opaque resource identity.</summary>
    /// <example><code>var valid = CataloguePaths.IsTarget(scopeId);</code></example>
    public static bool IsTarget(string? id) => CatalogueRules.IsIdentifier(id) && id is not (Current or Parent) &&
        !string.Equals(Separator + id, Deletions, StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(Separator + id, Antiforgery, StringComparison.OrdinalIgnoreCase);

    /// <summary>Constructs one exact details path from a validated catalogue kind and opaque identifier.</summary>
    /// <example><code>var path = CataloguePaths.Detail(CatalogueVersion.Scope, scopeId);</code></example>
    public static string Detail(string kind, string id) => IsTarget(id) ? Collection(kind) + Separator + Uri.EscapeDataString(id) :
        throw new ArgumentException(InvalidId, nameof(id));

    /// <summary>Constructs durable status independently of whether the target still exists.</summary>
    /// <example><code>var status = CataloguePaths.Job(CatalogueVersion.Scope, jobId);</code></example>
    public static string Job(string kind, Guid id) => id != Guid.Empty ? Collection(kind) + Deletions + Separator + id.ToString(GuidFormat) :
        throw new ArgumentException(InvalidId, nameof(id));
}
