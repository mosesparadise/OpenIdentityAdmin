using System.Globalization;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Observes real runtime database wait graphs without timers standing in for lock acquisition evidence.</summary>
internal static class DatabaseWaiters
{
    private const int WaitSeconds = 20;
    private const int CommandSeconds = 3;
    private const int PollMilliseconds = 50;
    private const string BlockerParameter = "blocker";
    private const string StageParameter = "stage";
    private const string SqlWaiters = "WITH blocked AS (SELECT session_id FROM sys.dm_exec_requests WHERE database_id = DB_ID() AND blocking_session_id = @blocker UNION ALL SELECT r.session_id FROM sys.dm_exec_requests r JOIN blocked b ON r.blocking_session_id = b.session_id WHERE r.database_id = DB_ID()) SELECT COUNT(DISTINCT b.session_id) FROM blocked b JOIN sys.dm_exec_sessions s ON s.session_id = b.session_id JOIN sys.dm_exec_requests r ON r.session_id = b.session_id CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE s.login_name = 'platform_runtime' AND t.text LIKE @stage";
    private const string PostgresWaiters = "WITH RECURSIVE blocked(pid) AS (SELECT pid FROM pg_stat_activity WHERE datname = current_database() AND @blocker = ANY(pg_blocking_pids(pid)) UNION SELECT a.pid FROM pg_stat_activity a JOIN blocked b ON b.pid = ANY(pg_blocking_pids(a.pid)) WHERE a.datname = current_database()) SELECT COUNT(*) FROM blocked b JOIN pg_stat_activity a USING(pid) WHERE a.usename = 'platform_runtime' AND a.query LIKE @stage";

    /// <summary>Finds the current transaction's provider-native session identity without disclosing connection settings.</summary>
    internal static async Task<int> IdentifyAsync(PlatformDbContext database, CancellationToken token)
    {
        const string SqlIdentity = "SELECT @@SPID";
        const string PostgresIdentity = "SELECT pg_backend_pid()";
        await using var command = database.Database.GetDbConnection().CreateCommand();
        command.Transaction = database.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = database.Database.IsSqlServer() ? SqlIdentity : PostgresIdentity;
        return Convert.ToInt32(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
    }

    /// <summary>Waits for the expected runtime statement to block behind the known session, under a bounded diagnostic deadline.</summary>
    internal static async Task ObserveAsync(BootstrapDatabase lease, int blocker, string stagePattern, int minimum)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(WaitSeconds));
        await using var scope = lease.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var connection = database.Database.GetDbConnection();
        await connection.OpenAsync(deadline.Token);
        while (true)
        {
            await using var command = connection.CreateCommand();
            command.CommandTimeout = CommandSeconds;
            command.CommandText = database.Database.IsSqlServer()
                ? SqlWaiters
                : PostgresWaiters;
            var parameter = command.CreateParameter();
            parameter.ParameterName = BlockerParameter;
            parameter.Value = blocker;
            command.Parameters.Add(parameter);
            var stage = command.CreateParameter();
            stage.ParameterName = StageParameter;
            stage.Value = stagePattern;
            command.Parameters.Add(stage);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(deadline.Token), System.Globalization.CultureInfo.InvariantCulture) >= minimum)
            {
                return;
            }
            await Task.Delay(PollMilliseconds, deadline.Token);
        }
    }
}
