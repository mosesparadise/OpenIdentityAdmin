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
    [Theory]
    [InlineData(DatabaseProvider.PostgreSql, Traits = ["Provider", "PostgreSql"])]
    [InlineData(DatabaseProvider.SqlServer, Traits = ["Provider", "SqlServer"])]
    public async Task Previous_schema_upgrade_preserves_legacy_interactions_and_restricts_budget_ownership(DatabaseProvider provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        var cancellationToken = deadline.Token;
        await using var postgres = new PostgreSqlBuilder(
            "postgres:18@sha256:74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336")
            .WithDatabase("identity_budget_upgrade").Build();
        await using var sqlServer = new MsSqlBuilder(
            "mcr.microsoft.com/mssql/server:2022-latest@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090").Build();
        string connection;
        if (provider == DatabaseProvider.PostgreSql)
        {
            await postgres.StartAsync(cancellationToken);
            connection = postgres.GetConnectionString();
        }
        else
        {
            await sqlServer.StartAsync(cancellationToken);
            connection = sqlServer.GetConnectionString();
        }
        var services = new ServiceCollection().AddLogging().AddPlatformPersistence(provider, connection, connection);
        await using var root = services.BuildServiceProvider(validateScopes: true);
        await using var scope = root.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await database.GetService<IMigrator>().MigrateAsync(SchemaCatalog.Platform(provider)[^2], cancellationToken);
        var id = new string('L', 43);
        var digest = new string('A', 64);
        var requestToken = "synthetic-legacy-token";
        var requestUri = "urn:synthetic:legacy";
        var client = "synthetic-client";
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "platform"."LoginInteraction"
                ("Id", "RequestTokenId", "RequestUri", "ClientId", "BrowserDigest", "CreatedAt", "ExpiresAt", "Consumed")
            VALUES ({id}, {requestToken}, {requestUri}, {client}, {digest}, {100L}, {400L}, {false})
            """, cancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, (await new SchemaReadinessCheck<PlatformDbContext>(root, SchemaCatalog.Platform(provider))
            .CheckHealthAsync(new(), cancellationToken)).Status);
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(root, cancellationToken));
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(root, cancellationToken));
        var legacy = await database.Set<LoginInteraction>().AsNoTracking().SingleAsync(value => value.Id == id, cancellationToken);
        Assert.True(legacy.BudgetId is null && legacy.RequestTokenId == requestToken && legacy.RequestUri == requestUri &&
            legacy.ClientId == client && legacy.BrowserDigest == digest && legacy.CreatedAt == 100 && legacy.ExpiresAt == 400 &&
            legacy.CompletedSessionId is null && legacy.CompletedUserId is null && !legacy.Consumed, "Migration changed an existing interaction or adopted budget ownership.");
        Assert.Equal(HealthStatus.Healthy, (await new SchemaReadinessCheck<PlatformDbContext>(root, SchemaCatalog.Platform(provider))
            .CheckHealthAsync(new(), cancellationToken)).Status);
        var budget = new LoginBrowserBudget { Id = digest, LastAdmissionAt = 100, ExpiresAt = 400 };
        database.Add(budget);
        var owned = new LoginInteraction
        {
            Id = new string('N', 43),
            RequestTokenId = "synthetic-new-token",
            RequestUri = "urn:synthetic:new",
            ClientId = client,
            BrowserDigest = digest,
            BudgetId = digest,
            CreatedAt = 100,
            ExpiresAt = 400
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
        Assert.Equal(2, await database.Set<LoginInteraction>().CountAsync(cancellationToken));
        Assert.True((await database.Set<LoginInteraction>().SingleAsync(value => value.Id == owned.Id, cancellationToken)).BudgetId == digest,
            "Foreign-key rejection changed the persisted, case-sensitive owner.");
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
