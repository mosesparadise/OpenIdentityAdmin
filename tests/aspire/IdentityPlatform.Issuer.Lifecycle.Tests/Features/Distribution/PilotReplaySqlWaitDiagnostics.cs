using System.Data.Common;
using System.Globalization;
using System.Text;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Distribution;

/// <summary>Retains bounded SQL wait classifications without exposing statements, parameters or session identities.</summary>
internal sealed class PilotReplaySqlWaitDiagnostics
{
    internal const string HolderParameter = "holder";
    private const int SnapshotLimit = 8;
    private const string ReportFormat = "pilot-sql-wait replay-phase={0} samples={1} distinct-overflow={2}";
    private const string Snapshot = "pilot-sql-wait runtime-requests={0} direct-blockers={1} lock-waiters={2} Gate={3} NativeTokenRead={4} Other={5} winner-fixture-blocked={6} winner-gate-transaction-X={7}";
    private const string RuntimeRequests = "RuntimeRequests";
    private const string DirectBlockers = "DirectBlockers";
    private const string LockWaiters = "LockWaiters";
    private const string Gate = "Gate";
    private const string NativeTokenRead = "NativeTokenRead";
    private const string Other = "Other";
    private const string WinnerFixtureBlocked = "WinnerFixtureBlocked";
    private const string WinnerGateExclusive = "WinnerGateExclusive";
    private static readonly CompositeFormat SnapshotFormat = CompositeFormat.Parse(Snapshot);
    private readonly HashSet<string> snapshots = [];
    private int samples;
    private bool overflow;

    // The waiter SELECT remains the sole success oracle; this adjacent sample is diagnostic only.
    internal const string Query = """
        ;WITH runtime_requests AS (
          SELECT r.blocking_session_id, r.wait_type,
            CASE WHEN LTRIM(a.ActiveStatement) LIKE 'UPDATE%' AND a.ActiveStatement LIKE '%PilotProtocolGate%' THEN 1
                 WHEN LTRIM(a.ActiveStatement) LIKE 'SELECT%' AND a.ActiveStatement LIKE '%OpenIddictTokens%' THEN 2
                 ELSE 0 END AS statement_class
          FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id
          OUTER APPLY sys.dm_exec_sql_text(r.sql_handle) t
          OUTER APPLY (SELECT SUBSTRING(t.text, r.statement_start_offset/2+1,
            (CASE WHEN r.statement_end_offset=-1 THEN DATALENGTH(t.text) ELSE r.statement_end_offset END-r.statement_start_offset)/2+1) AS ActiveStatement) a
          WHERE r.database_id=DB_ID() AND s.original_login_name='platform_runtime'
        )
        SELECT COUNT(*) AS RuntimeRequests,
          COALESCE(SUM(CASE WHEN blocking_session_id=@blocker THEN 1 ELSE 0 END),0) AS DirectBlockers,
          COALESCE(SUM(CASE WHEN blocking_session_id=@blocker AND wait_type LIKE 'LCK_M_%' THEN 1 ELSE 0 END),0) AS LockWaiters,
          COALESCE(SUM(CASE WHEN blocking_session_id=@blocker AND wait_type LIKE 'LCK_M_%' AND statement_class=1 THEN 1 ELSE 0 END),0) AS Gate,
          COALESCE(SUM(CASE WHEN blocking_session_id=@blocker AND wait_type LIKE 'LCK_M_%' AND statement_class=2 THEN 1 ELSE 0 END),0) AS NativeTokenRead,
          COALESCE(SUM(CASE WHEN blocking_session_id=@blocker AND wait_type LIKE 'LCK_M_%' AND statement_class=0 THEN 1 ELSE 0 END),0) AS Other,
          CAST(CASE WHEN EXISTS (SELECT 1 FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id
            WHERE r.session_id=@blocker AND r.database_id=DB_ID() AND s.original_login_name='platform_runtime'
              AND r.blocking_session_id=@holder AND r.wait_type LIKE 'LCK_M_%') THEN 1 ELSE 0 END AS bit) AS WinnerFixtureBlocked,
          CAST(CASE WHEN EXISTS (SELECT 1 FROM sys.dm_tran_locks l
            JOIN sys.dm_tran_session_transactions st ON st.session_id=l.request_session_id AND st.transaction_id=l.request_owner_id
            JOIN sys.dm_exec_requests wr ON wr.session_id=st.session_id AND wr.transaction_id=st.transaction_id
            WHERE l.request_session_id=@blocker AND l.resource_database_id=DB_ID()
              AND l.request_owner_type='TRANSACTION' AND l.request_status='GRANT' AND l.request_mode='X'
              AND ((l.resource_type='OBJECT' AND l.resource_associated_entity_id=OBJECT_ID(N'platform.PilotProtocolGate'))
                OR (l.resource_type IN ('KEY','PAGE','RID','HOBT') AND EXISTS (SELECT 1 FROM sys.partitions p
                  WHERE p.object_id=OBJECT_ID(N'platform.PilotProtocolGate') AND p.hobt_id=l.resource_associated_entity_id))))
            THEN 1 ELSE 0 END AS bit) AS WinnerGateExclusive
        FROM runtime_requests;
        """;

    /// <summary>Reads only the diagnostic result set from the existing bounded poll, never opening an extra observation window.</summary>
    internal async Task CaptureAsync(DbDataReader rows, CancellationToken token)
    {
        Assert.True(await rows.NextResultAsync(token));
        Assert.True(await rows.ReadAsync(token));
        var snapshot = string.Format(CultureInfo.InvariantCulture, SnapshotFormat,
            rows.GetInt32(rows.GetOrdinal(RuntimeRequests)), rows.GetInt32(rows.GetOrdinal(DirectBlockers)),
            rows.GetInt32(rows.GetOrdinal(LockWaiters)), rows.GetInt32(rows.GetOrdinal(Gate)),
            rows.GetInt32(rows.GetOrdinal(NativeTokenRead)), rows.GetInt32(rows.GetOrdinal(Other)),
            rows.GetBoolean(rows.GetOrdinal(WinnerFixtureBlocked)), rows.GetBoolean(rows.GetOrdinal(WinnerGateExclusive)));
        Assert.False(await rows.ReadAsync(token));
        samples++;
        if (snapshots.Count < SnapshotLimit || snapshots.Contains(snapshot))
        {
            snapshots.Add(snapshot);
        }
        else
        {
            overflow = true;
        }
    }

    /// <summary>Best-effort safe failure output preserves the original exception; absent samples never prove contention.</summary>
    internal void Report(bool gate)
    {
        try
        {
            TestContext.Current.TestOutputHelper?.WriteLine(ReportFormat, gate, samples, overflow);
            foreach (var snapshot in snapshots)
            {
                TestContext.Current.TestOutputHelper?.WriteLine(snapshot);
            }
        }
        catch (Exception) { /* Diagnostic output must not replace the original test failure. */ }
    }
}
