using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Proves the additive upgrade on real prior layout without pretending an old executable populated the fixture.</summary>
internal static class GovernanceUpgrade
{
    private const string PostgreSql = "PostgreSql";
    private const string PreviousPostgreSql = "20261012231221_AdministratorRecovery";
    private const string PreviousSqlServer = "20261012231226_AdministratorRecovery";
    private const string AuthoritySuffix = "_AdministratorAuthority";

    /// <summary>Uses native enrollment for nonempty authority, restores the prior layout, and verifies preservation through UP.</summary>
    internal static async Task RequireAsync(BootstrapFixture fixture, string provider)
    {
        await using var scenario = await GovernanceScenario.CreateAsync(fixture, provider);
        var lease = scenario.Journey.Lease;
        var token = TestContext.Current.CancellationToken;
        await using var scope = lease.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var expected = SchemaCatalog.Platform(DatabaseProviderParser.Parse(provider));
        Assert.Equal(expected, await database.Database.GetAppliedMigrationsAsync(token));
        await RequireReadyAsync(lease, expected, HealthStatus.Healthy);
        await RequirePopulatedAsync(database);
        var before = await GovernanceUpgradeSnapshot.ReadAsync(database);
        await database.GetService<IMigrator>().MigrateAsync(provider == PostgreSql ? PreviousPostgreSql : PreviousSqlServer, token);
        database.ChangeTracker.Clear();
        Assert.DoesNotContain(await database.Database.GetAppliedMigrationsAsync(token), migration => migration.EndsWith(AuthoritySuffix, StringComparison.Ordinal));
        Assert.Equal(before, await GovernanceUpgradeSnapshot.ReadAsync(database));
        await RequireReadyAsync(lease, expected, HealthStatus.Unhealthy);
        await database.Database.MigrateAsync(token);
        await database.Database.MigrateAsync(token);
        database.ChangeTracker.Clear();
        Assert.Equal(before, await GovernanceUpgradeSnapshot.ReadAsync(database));
        Assert.Equal(0, await database.Set<AdministratorGovernance>().Select(row => row.AuthorityRevision).SingleAsync(token));
        Assert.True(await database.Set<AdministratorGovernance>().Select(row => row.BootstrapConsumed).SingleAsync(token));
        Assert.Equal(expected, await database.Database.GetAppliedMigrationsAsync(token));
        await RequireReadyAsync(lease, expected, HealthStatus.Healthy);
    }

    /// <summary>Calls the actual bounded production readiness check through runtime credentials; no service command touches old layout.</summary>
    private static async Task RequireReadyAsync(BootstrapDatabase lease, IReadOnlyList<string> expected, HealthStatus status)
    {
        var result = await new SchemaReadinessCheck<PlatformDbContext>(lease.Runtime, expected)
            .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
        Assert.Equal(status, result.Status);
        Assert.Null(result.Exception);
    }

    /// <summary>Prevents vacuous preservation assertions over empty credential, session, recovery or bootstrap tables.</summary>
    private static async Task RequirePopulatedAsync(PlatformDbContext database)
    {
        var token = TestContext.Current.CancellationToken;
        Assert.NotEmpty(await database.Users.AsNoTracking().Select(row => row.Id).ToArrayAsync(token));
        Assert.NotEmpty(await database.Roles.AsNoTracking().Select(row => row.Id).ToArrayAsync(token));
        Assert.NotEmpty(await database.UserRoles.AsNoTracking().Select(row => row.UserId).ToArrayAsync(token));
        Assert.NotEmpty(await database.RoleClaims.AsNoTracking().Select(row => row.Id).ToArrayAsync(token));
        Assert.NotEmpty(await database.Set<AdministratorFactor>().Select(row => row.Id).ToArrayAsync(token));
        Assert.NotEmpty(await database.Set<AdministratorRecoveryCode>().Select(row => row.Id).ToArrayAsync(token));
        Assert.NotEmpty(await database.Set<AdministratorSession>().Select(row => row.Id).ToArrayAsync(token));
        Assert.Single(await database.Set<AdministratorBootstrapReceipt>().Select(row => row.Id).ToArrayAsync(token));
    }
}
