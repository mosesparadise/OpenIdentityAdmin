using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Preserves populated audit tables and exact migration histories from the immediate prior schema on both shipped providers.</summary>
public sealed class BrowserAuditMigrationTests
{
    private const string Postgres = "PostgreSql";
    private const string PostgresImage = "postgres:18@sha256:74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336";
    private const string SqlImage = "mcr.microsoft.com/mssql/server:2022-latest@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090";
    private const string Database = "browser_audit_upgrade";
    private const int DeadlineMinutes = 3;

    /// <summary>Rejects seeded forged additions and damaged old rows independently of any live database or migration result.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public void Given_IndependentAuditUpgradeWitness_When_SchemaOrHistoricalRowsAreForged_Then_EachDefectDenies(string provider)
    {
        AdministratorUpgradeWitnessControls.Verify();
        IssuerContinuationUpgradeWitnessControls.Verify(provider);
        BrowserAuditUpgradeWitnessControls.Verify(provider);
    }

    /// <summary>Native immediate-prior migrations populate both stores before the additive relation, preserving every old field and history on repeat upgrade.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_PopulatedPriorAuditSchemas_When_CorrelationMigrationsRun_Then_AllRowsAndHistoryRemainWithNullRelations(string provider)
    {
        BrowserAuditUpgradeWitnessControls.Verify(provider);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(DeadlineMinutes));
        await using var postgres = new PostgreSqlBuilder(PostgresImage).WithDatabase(Database).Build();
        await using var sql = new MsSqlBuilder(SqlImage).Build();
        var connection = await StartAsync(provider, postgres, sql, deadline.Token);
        var implementation = TestDatabaseProviders.Resolve(provider);
        var services = new ServiceCollection().AddLogging().AddPlatformPersistence(implementation, connection, connection);
        await using var root = services.BuildServiceProvider(validateScopes: true);
        await using var scope = root.CreateAsyncScope();
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var sessions = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        await PriorAsync(provider, platform, sessions, deadline.Token);
        await BrowserAuditMigrationRows.SeedAsync(platform, sessions, deadline.Token);
        var before = await BrowserAuditMigrationRows.CaptureAsync(platform, sessions, deadline.Token);
        var history = await BrowserAuditMigrationRows.CaptureHistoryAsync(platform, sessions, deadline.Token);
        string[][] additions = provider == Postgres
            ? [[PostgresPlatform, ManagedApplicationUpgradeWitness.PostgresMigration], [PostgresSessions]]
            : [[SqlPlatform, ManagedApplicationUpgradeWitness.SqlMigration], [SqlSessions]];
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(root, deadline.Token));
        await BrowserAuditMigrationRows.RequireAsync(platform, sessions, before, deadline.Token);
        await BrowserAuditMigrationRows.RequireHistoryAsync(platform, sessions, history, additions, deadline.Token);
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(root, deadline.Token));
        await BrowserAuditMigrationRows.RequireAsync(platform, sessions, before, deadline.Token);
        await BrowserAuditMigrationRows.RequireHistoryAsync(platform, sessions, history, additions, deadline.Token);
    }

    /// <summary>Targets the independently pinned preceding migrations without applying or reversing the candidate.</summary>
    private static async Task PriorAsync(string provider, PlatformDbContext platform, AdminSessionDbContext sessions, CancellationToken token)
    {
        await platform.GetService<IMigrator>().MigrateAsync(provider == Postgres ? PreviousPostgresPlatform : PreviousSqlPlatform, token);
        await sessions.GetService<IMigrator>().MigrateAsync(provider == Postgres ? PreviousPostgresSessions : PreviousSqlSessions, token);
    }

    /// <summary>Starts only the selected disposable engine; fixture ownership always joins and disposes the actual container.</summary>
    private static async Task<string> StartAsync(string provider, PostgreSqlContainer postgres, MsSqlContainer sql, CancellationToken token)
    {
        if (provider == Postgres)
        {
            await postgres.StartAsync(token);
            return postgres.GetConnectionString();
        }
        await sql.StartAsync(token);
        return new SqlConnectionStringBuilder(sql.GetConnectionString()) { InitialCatalog = Database }.ConnectionString;
    }
    private const string PreviousPostgresPlatform = "20261023223630_AdministratorIssuerContinuation";
    private const string PreviousSqlPlatform = "20261023223639_AdministratorIssuerContinuation";
    private const string PreviousPostgresSessions = "20261024000243_BrowserSessionCoordination";
    private const string PreviousSqlSessions = "20261024000342_BrowserSessionCoordination";
    private const string PostgresPlatform = "20261026113929_BrowserAuditCorrelation";
    private const string SqlPlatform = "20261026114108_BrowserAuditCorrelation";
    private const string PostgresSessions = "20261026113952_BrowserAuditCorrelation";
    private const string SqlSessions = "20261026114131_BrowserAuditCorrelation";
}
