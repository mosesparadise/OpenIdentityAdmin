using System.Data;
using System.Data.Common;
using System.Globalization;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Supplies engine-owned fixture setup and independently expected native permission/lock observations.</summary>
public sealed record NativeProviderFixture(IRelationalDatabaseProvider Provider,
    Func<DatabaseStore, string> RuntimeConnection, Func<CancellationToken, Task> PrepareRuntimePrincipals,
    Action<DbException> RequirePermissionDenied, Action<DbException> RequireLockTimeout, string ReadLockTimeout, string ExpectedBoundedLockTimeout);

/// <summary>Reusable native manager, readiness, least-privilege and nested connection ownership assertions for provider authors.</summary>
public static class NativeProviderConformance
{
    private const string UserName = "provider-conformance@example.test";
    private const string UpperClient = "ProviderConformanceClient";
    private const string LowerClient = "providerconformanceclient";
    private const string MissingClient = "PROVIDERCONFORMANCECLIENT";
    private const string SchemaDdl = "CREATE TABLE \"platform\".\"ProviderConformanceDenied\" (\"Id\" int)";
    private const string HistoryMutation = "INSERT INTO \"platform\".\"PlatformMigrations\" (\"MigrationId\", \"ProductVersion\") VALUES ('provider-conformance-denied', '10.0.12')";
    private const string ReadSessionHistory = "SELECT COUNT(*) FROM \"bff\".\"SessionMigrations\"";
    private const string ReadPlatformHistory = "SELECT COUNT(*) FROM \"platform\".\"PlatformMigrations\"";
    private const int OriginalCommandSeconds = 17;

    /// <summary>Runs unchanged assertions using a supplied implementation and disposable native fixture.</summary>
    public static async Task VerifyAsync(NativeProviderFixture fixture, CancellationToken token)
    {
        await fixture.PrepareRuntimePrincipals(token);
        var services = new ServiceCollection().AddLogging().AddPlatformPersistence(fixture.Provider,
            fixture.RuntimeConnection(DatabaseStore.Platform), fixture.RuntimeConnection(DatabaseStore.Sessions));
        await using var root = services.BuildServiceProvider(validateScopes: true);
        await using var scope = root.CreateAsyncScope();
        await VerifyManagersAsync(scope.ServiceProvider, token);
        await VerifyReadinessAsync(root, fixture.Provider, token);
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var sessions = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        await RequireDeniedAsync(platform, SchemaDdl, fixture, token);
        await RequireDeniedAsync(platform, HistoryMutation, fixture, token);
        await RequireDeniedAsync(platform, ReadSessionHistory, fixture, token);
        await RequireDeniedAsync(sessions, ReadPlatformHistory, fixture, token);
        await VerifyConnectionOwnershipAsync(platform, fixture, token);
        await NativeTransactionConformance.VerifyAsync(root, fixture, token);
    }

    /// <summary>Exercises manager-owned writes and independent ordinal native application lookup under runtime credentials.</summary>
    private static async Task VerifyManagersAsync(IServiceProvider services, CancellationToken token)
    {
        var users = services.GetRequiredService<UserManager<PlatformUser>>();
        var user = new PlatformUser { UserName = UserName };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        Assert.NotNull(await users.FindByIdAsync(user.Id));
        var applications = services.GetRequiredService<IOpenIddictApplicationManager>();
        foreach (var client in new[] { UpperClient, LowerClient })
        {
            await applications.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = client,
                ClientType = OpenIddictConstants.ClientTypes.Public
            }, token);
        }
        Assert.NotNull(await applications.FindByClientIdAsync(UpperClient, token));
        Assert.NotNull(await applications.FindByClientIdAsync(LowerClient, token));
        Assert.Null(await applications.FindByClientIdAsync(MissingClient, token));
    }

    /// <summary>Requires both runtime principals to read exact histories without administrator connections.</summary>
    private static async Task VerifyReadinessAsync(IServiceProvider services, IRelationalDatabaseProvider provider, CancellationToken token)
    {
        var platform = new SchemaReadinessCheck<PlatformDbContext>(services, provider.Migrations(DatabaseStore.Platform));
        var sessions = new SchemaReadinessCheck<AdminSessionDbContext>(services, provider.Migrations(DatabaseStore.Sessions));
        Assert.Equal(HealthStatus.Healthy, (await platform.CheckHealthAsync(new HealthCheckContext(), token)).Status);
        Assert.Equal(HealthStatus.Healthy, (await sessions.CheckHealthAsync(new HealthCheckContext(), token)).Status);
    }

    /// <summary>Requires a fixture-qualified native permission denial, never any arbitrary failure.</summary>
    private static async Task RequireDeniedAsync(DbContext database, string statement, NativeProviderFixture fixture, CancellationToken token)
    {
        var exception = await Assert.ThrowsAnyAsync<DbException>(() => database.Database.ExecuteSqlRawAsync(statement, token));
        fixture.RequirePermissionDenied(exception);
    }

    /// <summary>Tests actual nested leases, caller-owned open state, exact lock restoration and command timeout restoration.</summary>
    private static async Task VerifyConnectionOwnershipAsync(PlatformDbContext database, NativeProviderFixture fixture, CancellationToken token)
    {
        const int BoundedCommandSeconds = 5;
        database.Database.SetCommandTimeout(OriginalCommandSeconds);
        await database.Database.OpenConnectionAsync(token);
        var original = await ReadTimeoutAsync(database, fixture.ReadLockTimeout, token);
        await using (var outer = new CeremonyConnection(database))
        {
            await outer.OpenAsync(token);
            Assert.Equal(fixture.ExpectedBoundedLockTimeout, await ReadTimeoutAsync(database, fixture.ReadLockTimeout, token));
            await using (var inner = new CeremonyConnection(database))
            {
                await inner.OpenAsync(token);
            }
            Assert.Equal(fixture.ExpectedBoundedLockTimeout, await ReadTimeoutAsync(database, fixture.ReadLockTimeout, token));
            Assert.Equal(BoundedCommandSeconds, database.Database.GetCommandTimeout());
        }
        Assert.Equal(ConnectionState.Open, database.Database.GetDbConnection().State);
        Assert.Equal(original, await ReadTimeoutAsync(database, fixture.ReadLockTimeout, token));
        Assert.Equal(OriginalCommandSeconds, database.Database.GetCommandTimeout());
        await database.Database.CloseConnectionAsync();
    }

    /// <summary>Observes engine session state using fixture-owned SQL rather than the implementation's command builder.</summary>
    private static async Task<string> ReadTimeoutAsync(PlatformDbContext database, string query, CancellationToken token)
    {
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.CommandText = query;
        command.CommandTimeout = OriginalCommandSeconds;
        return Convert.ToString(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture)!;
    }
}
