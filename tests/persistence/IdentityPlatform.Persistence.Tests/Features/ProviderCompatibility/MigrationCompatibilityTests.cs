using System.Data.Common;
using Microsoft.Data.SqlClient;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Populates each engine with frozen38eb41e binaries, then uses the new implementation on those same databases.</summary>
public sealed class MigrationCompatibilityTests
{
    private const string PostgresImage = "postgres:18@sha256:74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336";
    private const string SqlImage = "mcr.microsoft.com/mssql/server:2022-latest@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090";
    private const string Postgres = "PostgreSql";
    private const string PostgresIndexes = "SELECT * FROM pg_indexes WHERE schemaname IN ('platform', 'bff')";
    private const string SqlIndexes = "SELECT s.name AS schema_name, t.name AS table_name, i.*, c.column_id, c.key_ordinal, c.partition_ordinal, c.is_descending_key, c.is_included_column FROM sys.indexes i JOIN sys.tables t ON i.object_id=t.object_id JOIN sys.schemas s ON t.schema_id=s.schema_id LEFT JOIN sys.index_columns c ON c.object_id=i.object_id AND c.index_id=i.index_id WHERE s.name IN ('platform', 'bff')";
    private const int DeadlineMinutes = 3;
    private const string PostgresIdentity = "SELECT system_identifier::text || ':' || current_database() FROM pg_control_system()";
    private const string SqlIdentity = "SELECT CAST(SERVERPROPERTY('ServerName') AS nvarchar(256)) + ':' + CAST(DB_ID() AS nvarchar(20))";

    /// <summary>Old-binary native data, every history row and physical schema survive repeated new-plugin migration.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_FrozenImplementationDatabase_When_ProviderPluginsUpgrade_Then_SchemaHistoryAndDataRemain(string name)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(DeadlineMinutes));
        await using var postgres = new PostgreSqlBuilder(PostgresImage).Build();
        await using var sqlServer = new MsSqlBuilder(SqlImage).Build();
        var connection = name == Postgres ? await StartPostgresAsync(postgres, deadline.Token) : await StartSqlServerAsync(sqlServer, deadline.Token);
        await BaselineProducer.PopulateAsync(name, connection, deadline.Token);
        await using DbConnection fixture = name == Postgres ? new NpgsqlConnection(connection) : new SqlConnection(connection);
        var indexQuery = name == Postgres ? PostgresIndexes : SqlIndexes;
        await DatabaseCompatibilityConformance.VerifyAsync(TestDatabaseProviders.Resolve(name), connection, fixture,
            (database, token) => DatabaseSnapshot.CaptureAsync(database, indexQuery, token),
            name == Postgres ? PostgresIdentity : SqlIdentity, deadline.Token);
        await NativeProviderConformance.VerifyAsync(NativeProviderFixtures.Create(name, connection), deadline.Token);
    }

    /// <summary>Starts only the selected disposable PostgreSQL fixture.</summary>
    private static async Task<string> StartPostgresAsync(PostgreSqlContainer container, CancellationToken token)
    {
        await container.StartAsync(token);
        return container.GetConnectionString();
    }

    /// <summary>Starts only the selected disposable SQL Server fixture.</summary>
    private static async Task<string> StartSqlServerAsync(MsSqlContainer container, CancellationToken token)
    {
        const string DatabaseName = "provider_compatibility";
        await container.StartAsync(token);
        return new SqlConnectionStringBuilder(container.GetConnectionString()) { InitialCatalog = DatabaseName }.ConnectionString;
    }
}
