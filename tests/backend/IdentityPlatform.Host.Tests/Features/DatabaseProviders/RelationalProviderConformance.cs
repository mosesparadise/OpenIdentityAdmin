using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Host.Tests.Features.DatabaseProviders;

/// <summary>Reusable public-contract checks accept any project-owned relational provider without identifying its vendor.</summary>
public static class RelationalProviderConformance
{
    /// <summary>Proves both stores use the supplied registration and retain separate schemas and native model ownership.</summary>
    public static void VerifyStoreConfiguration(IRelationalDatabaseProvider provider, string connection)
    {
        const string ExpectedPlatformSchema = "platform";
        const string ExpectedSessionSchema = "bff";
        const string IdentityTablePrefix = "AspNet";
        const int ExpectedCommandSeconds = 30;
        var platformOptions = new DbContextOptionsBuilder<PlatformDbContext>();
        platformOptions.UseDatabaseProvider(provider, connection, DatabaseStore.Platform).UseOpenIddict();
        var sessionOptions = new DbContextOptionsBuilder<AdminSessionDbContext>();
        sessionOptions.UseDatabaseProvider(provider, connection, DatabaseStore.Sessions).UseOpenIddict();
        using var platform = new PlatformDbContext(platformOptions.Options);
        using var sessions = new AdminSessionDbContext(sessionOptions.Options);
        Assert.Same(provider, platform.GetDatabaseProvider());
        Assert.Same(provider, sessions.GetDatabaseProvider());
        Assert.Equal(ExpectedPlatformSchema, platform.Model.GetDefaultSchema());
        Assert.Equal(ExpectedSessionSchema, sessions.Model.GetDefaultSchema());
        Assert.DoesNotContain(sessions.Model.GetEntityTypes(), entity => entity.GetTableName()!.StartsWith(IdentityTablePrefix, StringComparison.Ordinal));
        Assert.False(platform.Database.CreateExecutionStrategy().RetriesOnFailure);
        Assert.False(sessions.Database.CreateExecutionStrategy().RetriesOnFailure);
        Assert.Equal(ExpectedCommandSeconds, platform.Database.GetCommandTimeout());
        Assert.Equal(ExpectedCommandSeconds, sessions.Database.GetCommandTimeout());
    }

    /// <summary>Rejects both logical-store reversals before context initialization, migration or connection opening.</summary>
    public static void VerifyStoreIsolation(IRelationalDatabaseProvider provider, string connection)
    {
        var platform = new DbContextOptionsBuilder<PlatformDbContext>();
        platform.UseDatabaseProvider(provider, connection, DatabaseStore.Sessions);
        Assert.Throws<InvalidOperationException>(() => new PlatformDbContext(platform.Options));
        var sessions = new DbContextOptionsBuilder<AdminSessionDbContext>();
        sessions.UseDatabaseProvider(provider, connection, DatabaseStore.Platform);
        Assert.Throws<InvalidOperationException>(() => new AdminSessionDbContext(sessions.Options));
    }
}
