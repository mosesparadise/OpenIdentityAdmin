using Microsoft.Data.SqlClient;

namespace IdentityPlatform.Database.SqlServer;

/// <summary>Uses dedicated unpooled local administrator connections; production role provisioning remains operator-owned.</summary>
internal sealed class SqlServerLocalProvisioner : ILocalDatabaseProvisioner
{
    private const int CommandSeconds = 30;

    /// <summary>Validates fixed role inputs and provisions least-privilege runtime access without EF SQL diagnostics.</summary>
    public async Task ProvisionAsync(string connection, DatabaseStore store, string password, CancellationToken cancellationToken)
    {
        LocalRolePolicy.ValidatePassword(password);
        var sql = Statement(store, password);
        await using var isolated = new SqlConnection(new SqlConnectionStringBuilder(connection) { Pooling = false }.ConnectionString);
        await isolated.OpenAsync(cancellationToken);
        await using var command = isolated.CreateCommand();
        command.CommandTimeout = CommandSeconds;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>Uses only fixed role/schema/history identifiers and previously validated alphanumeric passwords.</summary>
    private static string Statement(DatabaseStore store, string password)
    {
        var role = LocalRolePolicy.Role(store);
        var schema = DatabaseStores.Schema(store);
        var history = DatabaseStores.History(store);
        const string RuntimeGrants = """
                IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = '{0}')
                    CREATE LOGIN [{0}] WITH PASSWORD = '{3}';
                ELSE
                    ALTER LOGIN [{0}] WITH PASSWORD = '{3}';
                IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '{0}')
                    CREATE USER [{0}] FOR LOGIN [{0}];
                GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[{1}] TO [{0}];
                DENY INSERT, UPDATE, DELETE ON OBJECT::[{1}].[{2}] TO [{0}];
                """;
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, RuntimeGrants, role, schema, history, password);
    }
}
