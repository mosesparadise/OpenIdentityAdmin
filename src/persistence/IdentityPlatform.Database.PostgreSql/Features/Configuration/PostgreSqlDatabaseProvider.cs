using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Database.PostgreSql;

/// <summary>Registers the existing PostgreSql implementation without changing its schemas or migration identities.</summary>
/// <example><code>var providers = new DatabaseProviders(new PostgreSqlDatabaseProvider());
/// services.AddIdentityPersistence(providers.Select(configuredProvider), runtimeConnection);</code></example>
public sealed class PostgreSqlDatabaseProvider : IRelationalDatabaseProvider
{
    public const string Identifier = "PostgreSql";
    private const string EfName = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private const string DesignConnection = "Host=localhost;Database=design_time_only";
    private const string MigrationAssembly = "IdentityPlatform.Migrations.PostgreSql";
    private const int CommandSeconds = 30;
    private static readonly IReadOnlyList<string> PlatformMigrations = Array.AsReadOnly<string>(["20261010012841_InitialPlatform", "20261010134307_FirstPartyLogin", "20261011131900_BrowserInteractionBudget", "20261012110309_AdministratorBootstrap", "20261012180735_AdministratorFactors", "20261012204719_AdministratorJourneys", "20261012231221_AdministratorRecovery", "20261013132308_AdministratorAuthority", "20261017224010_AdministratorProtocol", "20261023223630_AdministratorIssuerContinuation", "20261026113929_BrowserAuditCorrelation"]);
    private static readonly IReadOnlyList<string> SessionMigrations = Array.AsReadOnly<string>(["20261010011921_InitialSessions", "20261015123931_ProtectedSessionTickets", "20261017224013_AdministratorClientStateBudget", "20261024000243_BrowserSessionCoordination", "20261026113952_BrowserAuditCorrelation"]);
    public string Id => Identifier;
    public string EfProviderName => EfName;
    public string DesignTimeConnection => DesignConnection;
    public IDatabaseModelDialect Model { get; } = new PostgreSqlModelDialect();
    public IDatabaseConnectionPolicy Connections { get; } = new PostgreSqlConnectionPolicy();
    public ILocalDatabaseProvisioner LocalProvisioning { get; } = new PostgreSqlLocalProvisioner();

    /// <summary>Uses existing history/assembly identities and command bounds without execution retries.</summary>
    /// <example><code>options.UseDatabaseProvider(provider, connection, DatabaseStore.Platform);</code></example>
    public void Configure(DbContextOptionsBuilder options, string connection, DatabaseStore store)
    {
        ArgumentNullException.ThrowIfNull(options);
        Connections.Validate(connection);
        options.UseNpgsql(connection, database => database.MigrationsAssembly(MigrationAssembly)
            .MigrationsHistoryTable(DatabaseStores.History(store), DatabaseStores.Schema(store))
            .CommandTimeout(CommandSeconds));
    }

    /// <summary>Returns the immutable exact ordered compatibility catalog without loading migrations.</summary>
    /// <example><code>var expected = provider.Migrations(DatabaseStore.Platform);</code></example>
    public IReadOnlyList<string> Migrations(DatabaseStore store) => store switch
    {
        DatabaseStore.Platform => PlatformMigrations,
        DatabaseStore.Sessions => SessionMigrations,
        _ => throw new ArgumentOutOfRangeException(nameof(store))
    };
}
