using Npgsql;

namespace IdentityPlatform.Database.PostgreSql;

/// <summary>Uses dedicated unpooled local administrator connections; production role provisioning remains operator-owned.</summary>
internal sealed class PostgreSqlLocalProvisioner : ILocalDatabaseProvisioner
{
    private const int CommandSeconds = 30;

    /// <summary>Validates fixed role inputs and provisions least-privilege runtime access without EF SQL diagnostics.</summary>
    public async Task ProvisionAsync(string connection, DatabaseStore store, string password, CancellationToken cancellationToken)
    {
        LocalRolePolicy.ValidatePassword(password);
        var sql = Statement(store, password);
        await using var isolated = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connection) { Pooling = false }.ConnectionString);
        await isolated.OpenAsync(cancellationToken);
        await using var command = isolated.CreateCommand();
        command.CommandTimeout = CommandSeconds;
        const string SuppressSecretStatements = """
                SET log_statement = 'none';
                SET log_min_error_statement = 'panic';
                SET log_min_duration_statement = -1;
                SET log_min_duration_sample = -1;
                SET log_transaction_sample_rate = 0;
                SET log_error_verbosity = 'terse';
                """;
        command.CommandText = SuppressSecretStatements;
        await command.ExecuteNonQueryAsync(cancellationToken);
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
                DO $bootstrap$ BEGIN
                    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '{0}') THEN
                        CREATE ROLE {0} LOGIN PASSWORD '{3}';
                    ELSE
                        ALTER ROLE {0} PASSWORD '{3}';
                    END IF;
                END $bootstrap$;
                GRANT USAGE ON SCHEMA {1} TO {0};
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {1} TO {0};
                REVOKE INSERT, UPDATE, DELETE ON {1}."{2}" FROM {0};
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA {1} TO {0};
                """;
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, RuntimeGrants, role, schema, history, password);
    }
}
