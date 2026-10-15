using System.Data.Common;
using IdentityPlatform.Database;
using Npgsql;

namespace IdentityPlatform.Host.Tests.Features.DatabaseProviders;

/// <summary>Implements PostgreSql connection and lock mechanisms without owning the calling transaction.</summary>
internal sealed class ExternalConnectionPolicy : IDatabaseConnectionPolicy
{
    private const string InvalidConnection = "Invalid database connection configuration. An explicit server and database are required.";
    private const string IncompatibleConnection = "Database connection does not match the selected provider.";
    private const string Read = "SELECT current_setting('lock_timeout')";
    private const string Set = "SET lock_timeout = '5s'";
    private const string Restore = "SELECT set_config('lock_timeout', @previous, false)";
    private const string PreviousParameter = "previous";
    private const string TransactionLimit = "SET LOCAL lock_timeout = '5s'";
    public string? TransactionLockLimitSql => TransactionLimit;

    /// <summary>Rejects incomplete or malformed settings without retaining provider error text.</summary>
    public void Validate(string connection)
    {
        try
        {
            var value = new NpgsqlConnectionStringBuilder(connection);
            if (!string.IsNullOrWhiteSpace(value.Host) && !string.IsNullOrWhiteSpace(value.Database))
            {
                return;
            }
        }
        catch (ArgumentException) { /* Driver messages can contain secrets. */ }
        throw new ArgumentException(InvalidConnection);
    }

    /// <summary>Rejects foreign driver connections before any provider operation.</summary>
    public void RequireCompatible(DbConnection connection)
    {
        if (connection is not NpgsqlConnection)
        {
            throw new InvalidOperationException(IncompatibleConnection);
        }
    }

    /// <summary>Applies caller-owned connection deadlines and isolated readiness behavior.</summary>
    public void Bound(DbConnection connection, int seconds, bool readiness)
    {
        RequireCompatible(connection);
        var settings = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Timeout = seconds };
        if (readiness)
        {
            settings.Pooling = false;
        }
        connection.ConnectionString = settings.ConnectionString;
    }

    /// <summary>Reads only fixed schema-history identifiers in deterministic order.</summary>
    public string HistoryQuery(DatabaseStore store)
    {
        var schema = DatabaseStores.Schema(store);
        var table = DatabaseStores.History(store);
        const string OrderedHistoryQuery = "SELECT \"MigrationId\" FROM \"{0}\".\"{1}\" ORDER BY \"MigrationId\"";
        var query = string.Format(System.Globalization.CultureInfo.InvariantCulture, OrderedHistoryQuery, schema, table);
        return query;
    }

    /// <summary>Configures exact session timeout capture before applying a temporary bound.</summary>
    public void ReadLockTimeout(DbCommand command) => command.CommandText = Read;

    /// <summary>Configures the existing five-second lock bound.</summary>
    public void SetLockTimeout(DbCommand command) => command.CommandText = Set;

    /// <summary>Restores the exact captured provider value with safe conversion or binding.</summary>
    public void RestoreLockTimeout(DbCommand command, object previous)
    {
        command.CommandText = Restore;
        var parameter = command.CreateParameter();
        parameter.ParameterName = PreviousParameter;
        parameter.Value = previous;
        command.Parameters.Add(parameter);
    }

    /// <summary>Marks the physical session for discard; the caller closes it on every path.</summary>
    public void ClearPool(DbConnection connection)
    {
        RequireCompatible(connection);
        NpgsqlConnection.ClearPool((NpgsqlConnection)connection);
    }
}
