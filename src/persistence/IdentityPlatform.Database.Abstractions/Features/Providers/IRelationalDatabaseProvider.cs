using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Database;

/// <summary>Supplies immutable, trusted engine mechanisms without owning product transactions or authorization.</summary>
/// <example><code>var providers = new DatabaseProviders(new ProjectDatabaseProvider());
/// services.AddIdentityPersistence(providers.Select(configuredProvider), runtimeConnection);</code></example>
public interface IRelationalDatabaseProvider
{
    string Id
    {
        get;
    }
    string EfProviderName
    {
        get;
    }
    string DesignTimeConnection
    {
        get;
    }
    IDatabaseModelDialect Model
    {
        get;
    }
    IDatabaseConnectionPolicy Connections
    {
        get;
    }
    ILocalDatabaseProvisioner? LocalProvisioning
    {
        get;
    }

    /// <summary>Configures one relational driver, migration assembly/history and bounded commands without retries.</summary>
    /// <example><code>provider.Configure(options, configuredConnection, DatabaseStore.Platform);</code></example>
    void Configure(DbContextOptionsBuilder options, string connection, DatabaseStore store);

    /// <summary>Returns immutable ordered migration IDs without loading executable migration assemblies.</summary>
    /// <example><code>var expected = provider.Migrations(DatabaseStore.Sessions);</code></example>
    IReadOnlyList<string> Migrations(DatabaseStore store);
}
