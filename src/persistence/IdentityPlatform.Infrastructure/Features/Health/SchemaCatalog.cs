using IdentityPlatform.Database;

namespace IdentityPlatform.Infrastructure.Features.Health;

/// <summary>Reads registered runtime metadata without loading executable migrations.</summary>
/// <example><code>var expected = SchemaCatalog.Platform(provider);</code></example>
public static class SchemaCatalog
{
    /// <summary>Returns the complete ordered platform history for the selected provider.</summary>
    /// <example><code>var expected = SchemaCatalog.Platform(provider);</code></example>
    public static IReadOnlyList<string> Platform(IRelationalDatabaseProvider provider) => provider.Migrations(DatabaseStore.Platform);

    /// <summary>Returns the complete ordered session history for the selected provider.</summary>
    /// <example><code>var expected = SchemaCatalog.Sessions(provider);</code></example>
    public static IReadOnlyList<string> Sessions(IRelationalDatabaseProvider provider) => provider.Migrations(DatabaseStore.Sessions);
}
