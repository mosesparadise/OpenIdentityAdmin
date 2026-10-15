using IdentityPlatform.Database;
using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Reusable same-database upgrade assertions accept any registered implementation and fixture-owned catalog query.</summary>
public static class DatabaseCompatibilityConformance
{
    /// <summary>Requires complete physical schema, history and data preservation across repeated plugin upgrades.</summary>
    public static async Task VerifyAsync(IRelationalDatabaseProvider provider, string connection, DbConnection fixtureConnection,
        string indexQuery, string identityQuery, CancellationToken token)
    {
        const int SuccessfulUpgrade = 0;
        var before = await DatabaseSnapshot.CaptureAsync(fixtureConnection, indexQuery, token);
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
        Assert.Equal(provider.Migrations(DatabaseStore.Platform), await platform.Database.GetAppliedMigrationsAsync(token));
        Assert.Equal(provider.Migrations(DatabaseStore.Sessions), await sessions.Database.GetAppliedMigrationsAsync(token));
        Assert.Equal(SuccessfulUpgrade, await DatabaseUpgrade.RunAsync(root, token));
        Assert.Equal(SuccessfulUpgrade, await DatabaseUpgrade.RunAsync(root, token));
        var after = await DatabaseSnapshot.CaptureAsync(fixtureConnection, indexQuery, token);
        Assert.Equal(before, after);
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
