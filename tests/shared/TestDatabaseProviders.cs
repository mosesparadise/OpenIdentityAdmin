using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Database;
using IdentityPlatform.Database.PostgreSql;
using IdentityPlatform.Database.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Core.Features.Configuration
{
    /// <summary>Retains existing two-engine fixture identities; production provider selection has no closed enumeration.</summary>
    public enum DatabaseProvider
    {
        PostgreSql, SqlServer
    }
}

namespace IdentityPlatform.Testing
{
    /// <summary>Registers shipped providers solely for existing two-engine integration fixtures.</summary>
    internal static class TestDatabaseProviders
    {
        private static readonly DatabaseProviders Providers = new(new PostgreSqlDatabaseProvider(), new SqlServerDatabaseProvider());

        /// <summary>Resolves fixture identifiers through the public extensible registration contract.</summary>
        internal static IRelationalDatabaseProvider Resolve(string? name) => Providers.Select(name);

        /// <summary>Retains defined fixture semantics while rejecting invalid fixture values.</summary>
        internal static IRelationalDatabaseProvider Resolve(DatabaseProvider provider) => Providers.Select(provider.ToString());
    }

    /// <summary>Keeps legacy native fixture identities independent of production provider registration.</summary>
    internal static class TestProviderParser
    {
        /// <summary>Validates through the public registry, then maps only the test fixture's finite engine inventory.</summary>
        internal static DatabaseProvider Parse(string? value) => Enum.Parse<DatabaseProvider>(TestDatabaseProviders.Resolve(value).Id);
    }

    /// <summary>Adapts existing conformance fixtures to the instance-based public persistence API.</summary>
    internal static class TestPersistenceRegistration
    {
        /// <summary>Composes both existing fixture stores through explicitly registered implementations.</summary>
        internal static IServiceCollection AddPlatformPersistence(this IServiceCollection services, DatabaseProvider provider,
            string platformConnection, string sessionConnection) =>
            Infrastructure.Persistence.PersistenceRegistration.AddPlatformPersistence(services, TestDatabaseProviders.Resolve(provider), platformConnection, sessionConnection);

        /// <summary>Composes the fixture identity store through the public provider contract.</summary>
        internal static IServiceCollection AddIdentityPersistence(this IServiceCollection services, DatabaseProvider provider, string connection) =>
            Infrastructure.Persistence.PersistenceRegistration.AddIdentityPersistence(services, TestDatabaseProviders.Resolve(provider), connection);

        /// <summary>Composes the fixture session store through the public provider contract.</summary>
        internal static IServiceCollection AddSessionPersistence(this IServiceCollection services, DatabaseProvider provider, string connection) =>
            Infrastructure.Persistence.PersistenceRegistration.AddSessionPersistence(services, TestDatabaseProviders.Resolve(provider), connection);

        /// <summary>Configures fixture options, preserving explicit invalid-enum rejection at the fixture boundary.</summary>
        internal static void ConfigureDatabase(DbContextOptionsBuilder options, DatabaseProvider provider, string connection, string history)
        {
            if (!Enum.IsDefined(provider))
            {
                throw new ArgumentOutOfRangeException(nameof(provider));
            }
            Infrastructure.Persistence.PersistenceRegistration.ConfigureDatabase(options, TestDatabaseProviders.Resolve(provider), connection, history);
        }
    }

    /// <summary>Adapts existing native compatibility assertions to provider-owned runtime metadata.</summary>
    internal static class TestSchemaCatalog
    {
        /// <summary>Returns immutable platform history for a defined fixture engine.</summary>
        internal static IReadOnlyList<string> Platform(DatabaseProvider provider) => Catalog(provider, DatabaseStore.Platform);

        /// <summary>Returns immutable session history for a defined fixture engine.</summary>
        internal static IReadOnlyList<string> Sessions(DatabaseProvider provider) => Catalog(provider, DatabaseStore.Sessions);

        /// <summary>Rejects undefined fixture values before accessing registered compatibility metadata.</summary>
        private static IReadOnlyList<string> Catalog(DatabaseProvider provider, DatabaseStore store)
        {
            if (!Enum.IsDefined(provider))
            {
                throw new ArgumentOutOfRangeException(nameof(provider));
            }
            return TestDatabaseProviders.Resolve(provider).Migrations(store);
        }
    }
}
