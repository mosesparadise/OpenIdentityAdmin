using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace IdentityPlatform.Database.SqlServer;

/// <summary>Implements SqlServer connection and lock mechanisms without owning the calling transaction.</summary>
internal sealed class SqlServerConnectionPolicy : IDatabaseConnectionPolicy
{
    private const string InvalidConnection = "Invalid database connection configuration. An explicit server and database are required.";
    private const string IncompatibleConnection = "Database connection does not match the selected provider.";
    private const string Read = "SELECT @@LOCK_TIMEOUT";
    private const string Set = "SET LOCK_TIMEOUT 5000";
    private const string Restore = "SET LOCK_TIMEOUT ";
    public string? TransactionLockLimitSql => null;

    /// <summary>Rejects incomplete or malformed settings without retaining provider error text.</summary>
    public void Validate(string connection)
    {
        try
        {
            var value = new SqlConnectionStringBuilder(connection);
            if (!string.IsNullOrWhiteSpace(value.DataSource) && !string.IsNullOrWhiteSpace(value.InitialCatalog))
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
        if (connection is not SqlConnection)
        {
            throw new InvalidOperationException(IncompatibleConnection);
        }
    }

    /// <summary>Applies caller-owned connection deadlines and isolated readiness behavior.</summary>
    public void Bound(DbConnection connection, int seconds, bool readiness)
    {
        RequireCompatible(connection);
        var settings = new SqlConnectionStringBuilder(connection.ConnectionString) { ConnectTimeout = seconds };
        if (readiness)
        {
            settings.Pooling = false;
            settings.ConnectRetryCount = 0;
        }
        connection.ConnectionString = settings.ConnectionString;
    }

    /// <summary>Reads only fixed schema-history identifiers in deterministic order.</summary>
    public string HistoryQuery(DatabaseStore store)
    {
        var schema = DatabaseStores.Schema(store);
        var table = DatabaseStores.History(store);
        const string OrderedHistoryQuery = "SELECT [MigrationId] FROM [{0}].[{1}] ORDER BY [MigrationId]";
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
        command.CommandText = Restore + Convert.ToInt32(previous, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Marks the physical session for discard; the caller closes it on every path.</summary>
    public void ClearPool(DbConnection connection)
    {
        RequireCompatible(connection);
        SqlConnection.ClearPool((SqlConnection)connection);
    }
}
