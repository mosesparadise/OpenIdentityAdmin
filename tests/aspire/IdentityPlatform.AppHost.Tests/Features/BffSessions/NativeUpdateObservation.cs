using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.AppHost.Tests.Features.BffSessions;

/// <summary>Tags only test-owned native ticket updates and observes an actual provider lock wait without reading private parameters.</summary>
internal sealed class NativeUpdateObservation : DbCommandInterceptor
{
    private const string Update = "UPDATE";
    private const string TicketTable = "AdminSessionTicket";
    private const string Prefix = "/* bff-lock-test-";
    private const string Suffix = " */\n";
    private const string MarkerParameter = "marker";
    private const string Wildcard = "%";
    private const string PostgreSql = "SELECT COUNT(*) FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query LIKE @marker";
    private const string SqlServer = "SELECT COUNT(*) FROM sys.dm_exec_requests AS r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) AS t WHERE r.blocking_session_id > 0 AND t.text LIKE @marker";
    private static readonly TimeSpan ObservationInterval = TimeSpan.FromMilliseconds(10);
    private readonly string marker = Prefix + Guid.NewGuid().ToString() + Suffix;

    /// <summary>Adds a unique synthetic marker before native execution; production query semantics remain unchanged.</summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains(Update, StringComparison.Ordinal) && command.CommandText.Contains(TicketTable, StringComparison.Ordinal))
        {
            command.CommandText = marker + command.CommandText;
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Waits until the provider reports the marked request blocked on a real lock; case cancellation bounds observation.</summary>
    internal async Task RequireBlockedAsync(DbContext observer, CancellationToken cancellationToken)
    {
        await observer.Database.OpenConnectionAsync(cancellationToken);
        await using var command = observer.Database.GetDbConnection().CreateCommand();
        command.CommandText = observer.Database.IsNpgsql() ? PostgreSql : SqlServer;
        var parameter = command.CreateParameter();
        parameter.ParameterName = MarkerParameter;
        parameter.DbType = DbType.String;
        parameter.Value = marker + Wildcard;
        command.Parameters.Add(parameter);
        while (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) == 0)
        {
            await Task.Delay(ObservationInterval, cancellationToken);
        }
    }
}
