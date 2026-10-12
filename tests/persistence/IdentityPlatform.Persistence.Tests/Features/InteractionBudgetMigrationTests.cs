using System.Data.Common;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace IdentityPlatform.Persistence.Tests.Features;

public sealed class InteractionBudgetMigrationTests
{
    private const string PostgreSqlImage = "postgres:18@sha256:74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336";
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090";
    private const string DatabaseName = "identity_budget_upgrade";
    private const string PostgreSqlPrevious = "20261010134307_FirstPartyLogin";
    private const string SqlServerPrevious = "20261010134320_FirstPartyLogin";
    private const string BudgetMigrationSuffix = "_BrowserInteractionBudget";
    private const int ExpectedInteractions = 2;
    private const int DeadlineMinutes = 3;
    private const int IdentifierLength = 43;
    private const int DigestLength = 64;
    private const long CreatedAt = 100;
    private const long ExpiresAt = 400;
    private const string RequestToken = "synthetic-legacy-token";
    private const string RequestUri = "urn:synthetic:legacy";
    private const string Client = "synthetic-client";
    private const string NewToken = "synthetic-new-token";
    private const string NewUri = "urn:synthetic:new";
    private const string LegacyChanged = "Migration changed an existing interaction or adopted budget ownership.";
    private const string OwnerChanged = "Foreign-key rejection changed the persisted, case-sensitive owner.";

    /// <summary>Upgrades the fixed pre-budget schema, retaining legacy interactions and enforcing new restrictive ownership.</summary>
    [Theory]
    [InlineData(DatabaseProvider.PostgreSql, Traits = ["Provider", "PostgreSql"])]
    [InlineData(DatabaseProvider.SqlServer, Traits = ["Provider", "SqlServer"])]
    public async Task Given_PreBudgetSchema_When_Upgraded_Then_LegacyInteractionsAndRestrictiveOwnershipRemain(DatabaseProvider provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(DeadlineMinutes));
        var cancellationToken = deadline.Token;
        await using var postgres = new PostgreSqlBuilder(
            PostgreSqlImage)
            .WithDatabase(DatabaseName).Build();
        await using var sqlServer = new MsSqlBuilder(
            SqlServerImage).Build();
        var connection = await StartProviderAsync(provider, postgres, sqlServer, cancellationToken);
        var services = new ServiceCollection().AddLogging().AddPlatformPersistence(provider, connection, connection);
        await using var root = services.BuildServiceProvider(validateScopes: true);
        await using var scope = root.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await database.GetService<IMigrator>().MigrateAsync(provider == DatabaseProvider.PostgreSql ? PostgreSqlPrevious : SqlServerPrevious, cancellationToken);
        Assert.DoesNotContain(await database.Database.GetAppliedMigrationsAsync(cancellationToken), value => value.EndsWith(BudgetMigrationSuffix, StringComparison.Ordinal));
        await InsertLegacyAsync(database, cancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, (await new SchemaReadinessCheck<PlatformDbContext>(root, SchemaCatalog.Platform(provider))
            .CheckHealthAsync(new(), cancellationToken)).Status);
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(root, cancellationToken));
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(root, cancellationToken));
        await VerifyLegacyAsync(database, cancellationToken);
        Assert.Equal(HealthStatus.Healthy, (await new SchemaReadinessCheck<PlatformDbContext>(root, SchemaCatalog.Platform(provider))
            .CheckHealthAsync(new(), cancellationToken)).Status);
        await VerifyOwnershipAsync(database, provider, cancellationToken);
    }

    /// <summary>Starts only the selected real provider while the caller retains container disposal ownership.</summary>
    private static async Task<string> StartProviderAsync(DatabaseProvider provider, PostgreSqlContainer postgres, MsSqlContainer sqlServer, CancellationToken token)
    {
        if (provider == DatabaseProvider.PostgreSql)
        {
            await postgres.StartAsync(token);
            return postgres.GetConnectionString();
        }
        await sqlServer.StartAsync(token);
        return sqlServer.GetConnectionString();
    }

    /// <summary>Inserts a row using only columns that existed before browser budget ownership.</summary>
    private static async Task InsertLegacyAsync(PlatformDbContext database, CancellationToken cancellationToken)
    {
        var id = new string('L', IdentifierLength);
        var digest = new string('A', DigestLength);
        var requestToken = RequestToken;
        var requestUri = RequestUri;
        var client = Client;
        const string InsertLegacy = """
            INSERT INTO "platform"."LoginInteraction"
                ("Id", "RequestTokenId", "RequestUri", "ClientId", "BrowserDigest", "CreatedAt", "ExpiresAt", "Consumed")
            VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})
            """;
        await database.Database.ExecuteSqlRawAsync(InsertLegacy, [id, requestToken, requestUri, client, digest, CreatedAt, ExpiresAt, false], cancellationToken);
    }

    /// <summary>Checks preserved protocol and browser values without assigning a new budget to a legacy interaction.</summary>
    private static async Task VerifyLegacyAsync(PlatformDbContext database, CancellationToken cancellationToken)
    {
        var legacy = await database.Set<LoginInteraction>().AsNoTracking().SingleAsync(value => value.Id == new string('L', IdentifierLength), cancellationToken);
        Assert.True(legacy.BudgetId is null && legacy.RequestTokenId == RequestToken && legacy.RequestUri == RequestUri &&
            legacy.ClientId == Client && legacy.BrowserDigest == new string('A', DigestLength) && legacy.CreatedAt == CreatedAt && legacy.ExpiresAt == ExpiresAt &&
            legacy.CompletedSessionId is null && legacy.CompletedUserId is null && !legacy.Consumed, LegacyChanged);
    }

    /// <summary>Uses native DML to verify restrictive deletion and ordinal ownership independently of EF key comparers.</summary>
    private static async Task VerifyOwnershipAsync(PlatformDbContext database, DatabaseProvider provider, CancellationToken cancellationToken)
    {
        var digest = new string('A', DigestLength);
        var budget = new LoginBrowserBudget { Id = digest, LastAdmissionAt = CreatedAt, ExpiresAt = ExpiresAt };
        database.Add(budget);
        var owned = new LoginInteraction
        {
            Id = new string('N', IdentifierLength),
            RequestTokenId = NewToken,
            RequestUri = NewUri,
            ClientId = Client,
            BrowserDigest = digest,
            BudgetId = digest,
            CreatedAt = CreatedAt,
            ExpiresAt = ExpiresAt
        };
        database.Add(owned);
        await database.SaveChangesAsync(cancellationToken);
        var deleteError = await Assert.ThrowsAnyAsync<DbException>(() => database.Set<LoginBrowserBudget>().Where(value => value.Id == digest).ExecuteDeleteAsync(cancellationToken));
        RequireConstraintViolation(provider, deleteError, PostgresErrorCodes.RestrictViolation);
        var wrongCase = digest.ToLowerInvariant();
        Assert.Equal(0, await database.Set<LoginBrowserBudget>().CountAsync(value => value.Id == wrongCase, cancellationToken));
        // SQL Server's EF key comparer ignores case even with binary database collation.
        // Execute the update directly so the database, rather than change detection, rejects it.
        var ownerError = await Assert.ThrowsAnyAsync<DbException>(() => database.Set<LoginInteraction>().Where(value => value.Id == owned.Id)
            .ExecuteUpdateAsync(update => update.SetProperty(value => value.BudgetId, wrongCase), cancellationToken));
        RequireConstraintViolation(provider, ownerError, PostgresErrorCodes.ForeignKeyViolation);
        database.ChangeTracker.Clear();
        Assert.Equal(1, await database.Set<LoginBrowserBudget>().CountAsync(cancellationToken));
        Assert.Equal(ExpectedInteractions, await database.Set<LoginInteraction>().CountAsync(cancellationToken));
        Assert.True((await database.Set<LoginInteraction>().SingleAsync(value => value.Id == owned.Id, cancellationToken)).BudgetId == digest,
            OwnerChanged);
    }
    private static void RequireConstraintViolation(DatabaseProvider provider, DbException exception, string postgresCode)
    {
        // Provider error codes are safe diagnostics; exception text can contain row values.
        if (provider == DatabaseProvider.PostgreSql)
        {
            Assert.Equal(postgresCode, (exception as PostgresException)?.SqlState);
        }
        else
        {
            Assert.Equal(547, (exception as SqlException)?.Number);
        }
    }
}
