using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Uses fixture-only observer privileges to identify a real waiting session independently of product lock code.</summary>
internal sealed class NativeLockObservation(string provider, string connection)
{
    private const string PostgreSql = "PostgreSql";
    private const string SessionParameter = "session";
    private const string BlockerParameter = "blocker";
    private const string PgWait = "SELECT CASE WHEN @blocker = ANY(pg_blocking_pids(@session)) THEN 1 ELSE 0 END";
    private const string SqlWait = "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.dm_exec_requests WHERE session_id=@session AND blocking_session_id=@blocker AND wait_type LIKE 'LCK_M_%') THEN 1 ELSE 0 END";
    private const int ObserveSeconds = 2;
    private const int PollMilliseconds = 10;

    /// <summary>Captures the already-open native session that will attempt the peer profile lock.</summary>
    internal static int Session(DbConnection database) => database switch
    {
        NpgsqlConnection postgres => postgres.ProcessID,
        SqlConnection sql => sql.ServerProcessId,
        _ => throw new InvalidOperationException()
    };

    /// <summary>Requires an actual lock wait on that exact peer session within the unchanged five-second transaction budget.</summary>
    internal async Task WaitAsync(int session, int blocker, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(ObserveSeconds));
        await using DbConnection database = provider == PostgreSql ? new NpgsqlConnection(connection) : new SqlConnection(connection);
        await database.OpenAsync(deadline.Token);
        await using var command = database.CreateCommand();
        command.CommandTimeout = ObserveSeconds;
        command.CommandText = provider == PostgreSql ? PgWait : SqlWait;
        var parameter = command.CreateParameter();
        parameter.ParameterName = SessionParameter;
        parameter.Value = session;
        command.Parameters.Add(parameter);
        var blocking = command.CreateParameter();
        blocking.ParameterName = BlockerParameter;
        blocking.Value = blocker;
        command.Parameters.Add(blocking);
        while (Convert.ToInt32(await command.ExecuteScalarAsync(deadline.Token), CultureInfo.InvariantCulture) != 1)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(PollMilliseconds), deadline.Token);
        }
    }
}
