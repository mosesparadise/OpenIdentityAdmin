using IdentityPlatform.Core.Features.Configuration;

namespace IdentityPlatform.Infrastructure.Features.Health;

// Runtime services deliberately do not load executable migration assemblies.
// Architecture tests compare these compatibility contracts to the generated migrations.
/// <summary>Declares runtime schema readiness without loading executable migration assemblies.</summary>
/// <example><code>var required = SchemaCatalog.Platform(DatabaseProvider.PostgreSql);</code></example>
public static class SchemaCatalog
{
    private static readonly IReadOnlyList<string> SqlServerPlatform = Array.AsReadOnly<string>(["20261010012848_InitialPlatform", "20261010134320_FirstPartyLogin", "20261011131926_BrowserInteractionBudget", "20261012110314_AdministratorBootstrap", "20261012180753_AdministratorFactors", "20261012204848_AdministratorJourneys"]);

    private static readonly IReadOnlyList<string> PostgreSqlPlatform = Array.AsReadOnly<string>(["20261010012841_InitialPlatform", "20261010134307_FirstPartyLogin", "20261011131900_BrowserInteractionBudget", "20261012110309_AdministratorBootstrap", "20261012180735_AdministratorFactors", "20261012204719_AdministratorJourneys"]);

    /// <summary>Returns the complete ordered platform migration identities required before accepting runtime traffic.</summary>
    /// <example><code>var check = new SchemaReadinessCheck&lt;PlatformDbContext&gt;(services, SchemaCatalog.Platform(provider));</code></example>
    public static IReadOnlyList<string> Platform(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.PostgreSql => PostgreSqlPlatform,
        DatabaseProvider.SqlServer => SqlServerPlatform,
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    public static IReadOnlyList<string> Sessions(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.PostgreSql => ["20261010011921_InitialSessions"],
        DatabaseProvider.SqlServer => ["20261010011939_InitialSessions"],
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };
}
