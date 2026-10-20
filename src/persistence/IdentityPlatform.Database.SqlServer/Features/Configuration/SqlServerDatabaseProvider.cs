using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Database.SqlServer;

/// <summary>Registers the existing SqlServer implementation without changing its schemas or migration identities.</summary>
/// <example><code>var providers = new DatabaseProviders(new SqlServerDatabaseProvider());
/// services.AddIdentityPersistence(providers.Select(configuredProvider), runtimeConnection);</code></example>
public sealed class SqlServerDatabaseProvider : IRelationalDatabaseProvider
{
    public const string Identifier = "SqlServer";
    private const string EfName = "Microsoft.EntityFrameworkCore.SqlServer";
    private const string DesignConnection = "Server=localhost;Database=design_time_only;Integrated Security=true";
    private const string MigrationAssembly = "IdentityPlatform.Migrations.SqlServer";
    private const int CommandSeconds = 30;
    private static readonly IReadOnlyList<string> PlatformMigrations = Array.AsReadOnly<string>(["20261010012848_InitialPlatform", "20261010134320_FirstPartyLogin", "20261011131926_BrowserInteractionBudget", "20261012110314_AdministratorBootstrap", "20261012180753_AdministratorFactors", "20261012204848_AdministratorJourneys", "20261012231226_AdministratorRecovery", "20261013132412_AdministratorAuthority", "20261017224016_AdministratorProtocol"]);
    private static readonly IReadOnlyList<string> SessionMigrations = Array.AsReadOnly<string>(["20261010011939_InitialSessions", "20261015123934_ProtectedSessionTickets", "20261017224019_AdministratorClientStateBudget"]);
    public string Id => Identifier;
    public string EfProviderName => EfName;
    public string DesignTimeConnection => DesignConnection;
    public IDatabaseModelDialect Model { get; } = new SqlServerModelDialect();
    public IDatabaseConnectionPolicy Connections { get; } = new SqlServerConnectionPolicy();
    public ILocalDatabaseProvisioner LocalProvisioning { get; } = new SqlServerLocalProvisioner();

    /// <summary>Uses existing history/assembly identities and command bounds without execution retries.</summary>
    /// <example><code>options.UseDatabaseProvider(provider, connection, DatabaseStore.Platform);</code></example>
    public void Configure(DbContextOptionsBuilder options, string connection, DatabaseStore store)
    {
        ArgumentNullException.ThrowIfNull(options);
        Connections.Validate(connection);
        options.UseSqlServer(connection, database => database.MigrationsAssembly(MigrationAssembly)
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
