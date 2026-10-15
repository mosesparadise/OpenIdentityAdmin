using System.Data.Common;

namespace IdentityPlatform.Database;

/// <summary>Provides engine operations; callers retain deadline, connection, transaction and disposal ownership.</summary>
/// <example><code>provider.Connections.Bound(connection, seconds: 3, readiness: true);</code></example>
public interface IDatabaseConnectionPolicy
{
    string? TransactionLockLimitSql
    {
        get;
    }

    /// <summary>Rejects malformed or incomplete connection strings without echoing values.</summary>
    /// <example><code>provider.Connections.Validate(configuredConnection);</code></example>
    void Validate(string connection);

    /// <summary>Rejects a connection belonging to a different driver.</summary>
    /// <example><code>provider.Connections.RequireCompatible(context.Database.GetDbConnection());</code></example>
    void RequireCompatible(DbConnection connection);

    /// <summary>Bounds connect time; readiness also disables pooling and connection retries.</summary>
    /// <example><code>provider.Connections.Bound(connection, 3, readiness: true);</code></example>
    void Bound(DbConnection connection, int seconds, bool readiness);

    /// <summary>Returns a read-only, ordered history query for a fixed logical store.</summary>
    /// <example><code>command.CommandText = provider.Connections.HistoryQuery(DatabaseStore.Platform);</code></example>
    string HistoryQuery(DatabaseStore store);

    /// <summary>Configures capture of the exact current session lock timeout.</summary>
    /// <example><code>provider.Connections.ReadLockTimeout(command); var previous = await command.ExecuteScalarAsync(token);</code></example>
    void ReadLockTimeout(DbCommand command);

    /// <summary>Configures the product's bounded session lock timeout.</summary>
    /// <example><code>provider.Connections.SetLockTimeout(command); await command.ExecuteNonQueryAsync(token);</code></example>
    void SetLockTimeout(DbCommand command);

    /// <summary>Configures exact restoration of a previously captured value.</summary>
    /// <example><code>provider.Connections.RestoreLockTimeout(command, previous); await command.ExecuteNonQueryAsync(cleanupToken);</code></example>
    void RestoreLockTimeout(DbCommand command, object previous);

    /// <summary>Marks an unsafe physical pooled session for discard; caller must also close the connection.</summary>
    /// <example><code>try { provider.Connections.ClearPool(connection); } finally { await connection.CloseAsync(); }</code></example>
    void ClearPool(DbConnection connection);
}
