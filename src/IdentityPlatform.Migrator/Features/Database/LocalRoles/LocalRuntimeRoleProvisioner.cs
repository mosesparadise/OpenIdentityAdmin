using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using Npgsql;
using Microsoft.Data.SqlClient;

namespace IdentityPlatform.Migrator.Features.Database.LocalRoles;

public enum RuntimeStore { Platform, Sessions }

public sealed record LocalRuntimeCredentials(string PlatformPassword, string SessionsPassword);

// Only invoked by the explicit Development-only Aspire bootstrap. Production principals
// are provisioned by the operator, outside the application/migration lifecycle.
public static class LocalRuntimeRoleProvisioner
{
    public static void ValidatePassword(string password)
    {
        if (password.Length is < 32 or > 128 || !password.All(char.IsAsciiLetterOrDigit) ||
            !password.Any(char.IsAsciiLetterUpper) || !password.Any(char.IsAsciiLetterLower) ||
            !password.Any(char.IsAsciiDigit))
            throw new ArgumentException("Invalid local runtime password format.", nameof(password));
    }

    public static async Task ProvisionAsync(DbContext database, RuntimeStore store,
        string password, CancellationToken cancellationToken)
    {
        // Aspire generates alphanumeric local passwords. A strict alphabet makes the DDL
        // literals safe where providers cannot bind password/identifier parameters.
        ValidatePassword(password);

        var (role, schema, history) = store switch
        {
            RuntimeStore.Platform => ("platform_runtime", "platform", "PlatformMigrations"),
            RuntimeStore.Sessions => ("sessions_runtime", "bff", "SessionMigrations"),
            _ => throw new ArgumentOutOfRangeException(nameof(store))
        };
        string sql;
        if (database.Database.IsNpgsql())
        {
            sql = $"""
                DO $bootstrap$ BEGIN
                    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = '{role}') THEN
                        CREATE ROLE {role} LOGIN PASSWORD '{password}';
                    ELSE
                        ALTER ROLE {role} PASSWORD '{password}';
                    END IF;
                END $bootstrap$;
                GRANT USAGE ON SCHEMA {schema} TO {role};
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {schema} TO {role};
                REVOKE INSERT, UPDATE, DELETE ON {schema}."{history}" FROM {role};
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA {schema} TO {role};
                """;
        }
        else if (database.Database.IsSqlServer())
        {
            sql = $"""
                IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = '{role}')
                    CREATE LOGIN [{role}] WITH PASSWORD = '{password}';
                ELSE
                    ALTER LOGIN [{role}] WITH PASSWORD = '{password}';
                IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '{role}')
                    CREATE USER [{role}] FOR LOGIN [{role}];
                GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[{schema}] TO [{role}];
                DENY INSERT, UPDATE, DELETE ON OBJECT::[{schema}].[{history}] TO [{role}];
                """;
        }
        else throw new InvalidOperationException("Unsupported local database provider.");

        // A dedicated, unpooled bootstrap connection avoids EF command diagnostics
        // and prevents local logging overrides leaking into another pooled operation.
        await using DbConnection connection = database.Database.IsNpgsql()
            ? new NpgsqlConnection(new NpgsqlConnectionStringBuilder(database.Database.GetConnectionString())
                { Pooling = false }.ConnectionString)
            : new SqlConnection(new SqlConnectionStringBuilder(database.Database.GetConnectionString())
                { Pooling = false }.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 30;
        if (database.Database.IsNpgsql())
        {
            // Development bootstrap uses the container administrator. If these protections
            // cannot be set, fail before sending any password-bearing statement. Error
            // severity/message remain visible; statement text and verbose context do not.
            command.CommandText = """
                SET log_statement = 'none';
                SET log_min_error_statement = 'panic';
                SET log_min_duration_statement = -1;
                SET log_min_duration_sample = -1;
                SET log_transaction_sample_rate = 0;
                SET log_error_verbosity = 'terse';
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
        // Disposing the unpooled connection discards every session setting on all paths.
    }
}
