using System.Data.Common;
using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Reusable same-database upgrade assertions accept any registered implementation and fixture-owned physical fingerprint.</summary>
public static class DatabaseCompatibilityConformance
{
    /// <summary>Requires an independent approved-addition witness and exact idempotence after upgrading the same physical database.</summary>
    public static async Task VerifyAsync(IRelationalDatabaseProvider provider, string connection, DbConnection fixtureConnection,
        Func<DbConnection, CancellationToken, Task<string>> captureFingerprint, Func<CancellationToken, Task> verifyApprovedAdditions,
        string identityQuery, CancellationToken token)
    {
        const int SuccessfulUpgrade = 0;
        var before = await captureFingerprint(fixtureConnection, token);
        Assert.False(string.IsNullOrWhiteSpace(before));
        var physicalIdentity = await IdentityAsync(fixtureConnection, identityQuery, token);
        var services = new ServiceCollection().AddLogging();
        services.AddPlatformPersistence(provider, connection, connection);
        await using var root = services.BuildServiceProvider(validateScopes: true);
        await using var scope = root.CreateAsyncScope();
        var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var sessions = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        await RequireIdentityAsync(fixtureConnection, platform.Database.GetDbConnection(), physicalIdentity, identityQuery, token);
        await RequireIdentityAsync(fixtureConnection, sessions.Database.GetDbConnection(), physicalIdentity, identityQuery, token);
        Assert.False(platform.Database.HasPendingModelChanges());
        Assert.False(sessions.Database.HasPendingModelChanges());
        Assert.Equal(provider.Migrations(DatabaseStore.Platform).SkipLast(1), await platform.Database.GetAppliedMigrationsAsync(token));
        Assert.Equal(provider.Migrations(DatabaseStore.Sessions).SkipLast(1), await sessions.Database.GetAppliedMigrationsAsync(token));
        Assert.Equal(SuccessfulUpgrade, await DatabaseUpgrade.RunAsync(root, token));
        await verifyApprovedAdditions(token);
        Assert.Equal(provider.Migrations(DatabaseStore.Platform), await platform.Database.GetAppliedMigrationsAsync(token));
        Assert.Equal(provider.Migrations(DatabaseStore.Sessions), await sessions.Database.GetAppliedMigrationsAsync(token));
        var upgraded = await captureFingerprint(fixtureConnection, token);
        Assert.NotEqual(before, upgraded);
        Assert.Equal(SuccessfulUpgrade, await DatabaseUpgrade.RunAsync(root, token));
        var after = await captureFingerprint(fixtureConnection, token);
        Assert.Equal(upgraded, after);
    }

    /// <summary>Binds each plugin context to the independently created fixture endpoint, database and native identity.</summary>
    private static async Task RequireIdentityAsync(DbConnection fixture, DbConnection actual, object identity, string query, CancellationToken token)
    {
        Assert.Equal(fixture.DataSource, actual.DataSource);
        Assert.Equal(fixture.Database, actual.Database);
        Assert.Equal(identity, await IdentityAsync(actual, query, token));
    }

    /// <summary>Reads a fixture-owned cluster/database identity query without provider-selected SQL.</summary>
    private static async Task<object> IdentityAsync(DbConnection connection, string query, CancellationToken token)
    {
        const int CommandSeconds = 30;
        await connection.OpenAsync(token);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandTimeout = CommandSeconds;
            command.CommandText = query;
            return (await command.ExecuteScalarAsync(token))!;
        }
        finally { await connection.CloseAsync(); }
    }
}
