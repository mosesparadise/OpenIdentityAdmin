using IdentityPlatform.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.Data.SqlClient;

namespace IdentityPlatform.Host.Tests.Features.DatabaseProviders;

/// <summary>Runs shared contract checks against both migrated production implementations.</summary>
public sealed class ProviderConformanceTests
{
    private const string Unknown = "unregistered-secret";
    private const int BoundedSeconds = 3;

    /// <summary>Both shipped implementations satisfy identical configuration and store-isolation assertions.</summary>
    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void Given_RegisteredProvider_When_BothStoresAreConfigured_Then_SharedConformanceHolds(string name)
    {
        var provider = TestDatabaseProviders.Resolve(name);
        RelationalProviderConformance.VerifyStoreConfiguration(provider, provider.DesignTimeConnection);
        RelationalProviderConformance.VerifyStoreIsolation(provider, provider.DesignTimeConnection);
    }

    /// <summary>Selection normalizes only known names and fails safely for unknown or ambiguous registrations.</summary>
    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void Given_ExplicitRegistrations_When_Selected_Then_OnlyCanonicalUniqueInstancesResolve(string name)
    {
        var provider = TestDatabaseProviders.Resolve(name);
        var catalog = new IdentityPlatform.Database.DatabaseProviders(provider);
        Assert.Same(provider, catalog.Select(name.ToUpperInvariant()));
        Assert.Same(provider, catalog.Select(string.Concat(' ', name, ' ')));
        var exception = Assert.Throws<ArgumentException>(() => catalog.Select(Unknown));
        Assert.DoesNotContain(Unknown, exception.ToString());
        Assert.Throws<ArgumentException>(() => catalog.Select(null));
        Assert.Throws<ArgumentException>(() => new IdentityPlatform.Database.DatabaseProviders(provider, provider));
        Assert.Throws<ArgumentException>(() => new IdentityPlatform.Database.DatabaseProviders());
        Assert.Throws<ArgumentException>(() => new IdentityPlatform.Database.DatabaseProviders([null!]));
    }

    /// <summary>Undefined logical stores cannot choose a schema, history, catalog or driver configuration.</summary>
    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void Given_UndefinedStore_When_ProviderMetadataIsRequested_Then_Rejects(string name)
    {
        const DatabaseStore invalid = (DatabaseStore)99;
        var provider = TestDatabaseProviders.Resolve(name);
        Assert.Throws<ArgumentOutOfRangeException>(() => DatabaseStores.Schema(invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => DatabaseStores.History(invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => LocalRolePolicy.Role(invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => DatabaseStores.FromHistory(Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.Migrations(invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.Connections.HistoryQuery(invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DbContextOptionsBuilder().UseDatabaseProvider(provider, provider.DesignTimeConnection, invalid));
    }

    /// <summary>Driver-specific helpers reject foreign connections before rewriting settings or clearing pools.</summary>
    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void Given_ForeignConnection_When_PolicyIsApplied_Then_RejectsBeforeUse(string name)
    {
        var provider = TestDatabaseProviders.Resolve(name);
        const string PostgresFixture = "PostgreSql";
        using System.Data.Common.DbConnection foreign = name == PostgresFixture ? new SqlConnection() : new NpgsqlConnection();
        Assert.Throws<InvalidOperationException>(() => provider.Connections.RequireCompatible(foreign));
        Assert.Throws<InvalidOperationException>(() => provider.Connections.Bound(foreign, BoundedSeconds, readiness: true));
        Assert.Throws<InvalidOperationException>(() => provider.Connections.ClearPool(foreign));
    }
}
