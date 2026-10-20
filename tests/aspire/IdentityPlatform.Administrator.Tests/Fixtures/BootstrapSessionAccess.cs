using System.Data.Common;
using IdentityPlatform.Core.Features.Configuration;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Changes only one disposable Sessions table's runtime read permission, retaining provider-specific denial evidence.</summary>
internal sealed class BootstrapSessionAccess(DatabaseProvider provider, string administrator, string runtime)
{
    private const int CommandSeconds = 5;
    private const int SqlPermissionDenied = 229;
    private const string PgPermissionDenied = "42501";
    private const string PgRead = "SELECT COUNT(*) FROM bff.\"OpenIddictTokens\"";
    private const string SqlRead = "SELECT COUNT(*) FROM [bff].[OpenIddictTokens]";
    private const string PgAllow = "GRANT SELECT ON bff.\"OpenIddictTokens\" TO sessions_runtime";
    private const string PgDeny = "REVOKE SELECT ON bff.\"OpenIddictTokens\" FROM sessions_runtime";
    private const string SqlAllow = "REVOKE SELECT ON OBJECT::[bff].[OpenIddictTokens] FROM [sessions_runtime]";
    private const string SqlDeny = "DENY SELECT ON OBJECT::[bff].[OpenIddictTokens] TO [sessions_runtime]";

    /// <summary>Restores PostgreSQL's direct table grant or removes SQL Server's object denial to recover its existing schema grant.</summary>
    internal Task SetReadAsync(bool allow, CancellationToken token) => ExecuteAsync(administrator,
        provider == DatabaseProvider.PostgreSql ? allow ? PgAllow : PgDeny : allow ? SqlAllow : SqlDeny, token);

    /// <summary>Uses a fresh actual runtime connection for an independent positive or exact permission-denial witness.</summary>
    internal Task ReadAsync(CancellationToken token) => ExecuteAsync(runtime, provider == DatabaseProvider.PostgreSql ? PgRead : SqlRead, token);

    /// <summary>Matches actual provider denial codes through nested failures rather than crediting arbitrary unavailable responses.</summary>
    internal bool IsDenied(Exception? error) => error is not null &&
        (provider == DatabaseProvider.PostgreSql && error is PostgresException postgres && postgres.SqlState == PgPermissionDenied ||
        provider == DatabaseProvider.SqlServer && error is SqlException sql && sql.Number == SqlPermissionDenied ||
        error is AggregateException aggregate && aggregate.InnerExceptions.Any(IsDenied) || IsDenied(error.InnerException));

    /// <summary>Runs a fixed fixture statement with bounded connection and command ownership; connection strings never enter diagnostic output.</summary>
    private async Task ExecuteAsync(string connection, string text, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(CommandSeconds));
        await using DbConnection database = provider == DatabaseProvider.PostgreSql ? new NpgsqlConnection(connection) : new SqlConnection(connection);
        await database.OpenAsync(deadline.Token);
        await using var command = database.CreateCommand();
        command.CommandTimeout = CommandSeconds;
        command.CommandText = text;
        await command.ExecuteScalarAsync(deadline.Token);
    }
}
