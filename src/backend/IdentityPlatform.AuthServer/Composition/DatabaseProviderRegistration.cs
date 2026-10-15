using IdentityPlatform.Database;
using IdentityPlatform.Database.PostgreSql;
using IdentityPlatform.Database.SqlServer;

namespace IdentityPlatform.AuthServer.Composition;

/// <summary>Registers the database implementations explicitly referenced by this host project.</summary>
internal static class DatabaseProviderRegistration
{
    internal static DatabaseProviders Providers { get; } = new(new PostgreSqlDatabaseProvider(), new SqlServerDatabaseProvider());
}
