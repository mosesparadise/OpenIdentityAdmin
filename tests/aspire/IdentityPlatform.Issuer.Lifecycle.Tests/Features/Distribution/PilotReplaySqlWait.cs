namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Distribution;

/// <summary>Names only positively observed ordering evidence without disclosing native session or credential identifiers.</summary>
internal enum PilotReplaySqlWaitMode
{
    FixtureInsert, GateWait, NativeTokenReadMatched
}

/// <summary>Correlates a known issuer process with the gate wait or an exact native read lock held by the same generation transaction.</summary>
internal static class PilotReplaySqlWait
{
    internal const string WinnerProcessParameter = "winnerProcess";
    internal const string ReplayProcessParameter = "replayProcess";
    internal const string ObservedMode = "pilot-sql-ordering witness={0}";
    internal const string Query = """
        WITH winner AS (
          SELECT r.session_id, r.transaction_id FROM sys.dm_exec_requests r
          JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id
          WHERE @gate=1 AND r.session_id=@blocker AND r.database_id=DB_ID()
            AND s.original_login_name='platform_runtime' AND s.host_process_id=@winnerProcess
            AND r.blocking_session_id=@holder AND r.wait_type LIKE 'LCK_M_%'
        ), winner_locks AS (
          SELECT l.* FROM sys.dm_tran_locks l
          JOIN winner w ON w.session_id=l.request_session_id AND w.transaction_id=l.request_owner_id
          JOIN sys.dm_tran_session_transactions st ON st.session_id=w.session_id AND st.transaction_id=w.transaction_id
          WHERE l.resource_database_id=DB_ID() AND l.request_owner_type='TRANSACTION'
            AND l.request_status='GRANT' AND l.request_mode='X'
        )
        SELECT CAST(r.session_id AS int),
          CASE WHEN @gate=0 THEN 0 WHEN t.text LIKE '%PilotProtocolGate%' THEN 1 ELSE 2 END AS ObservedMode
        FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id
        CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
        CROSS APPLY (SELECT SUBSTRING(t.text, r.statement_start_offset/2+1,
          (CASE WHEN r.statement_end_offset=-1 THEN DATALENGTH(t.text) ELSE r.statement_end_offset END-r.statement_start_offset)/2+1) AS ActiveStatement) a
        WHERE r.database_id=DB_ID() AND s.original_login_name='platform_runtime'
          AND s.host_process_id=CASE WHEN @gate=0 THEN @winnerProcess ELSE @replayProcess END
          AND r.blocking_session_id=@blocker AND r.wait_type LIKE 'LCK_M_%'
          AND (@gate=0 OR t.text LIKE '%PilotProtocolGate%' OR (
            LTRIM(a.ActiveStatement) LIKE 'SELECT%' AND a.ActiveStatement LIKE '%OpenIddictTokens%'
            AND EXISTS (SELECT 1 FROM winner_locks g
              WHERE (g.resource_type='OBJECT' AND g.resource_associated_entity_id=OBJECT_ID(N'platform.PilotProtocolGate'))
                OR (g.resource_type IN ('KEY','PAGE','RID','HOBT') AND EXISTS (SELECT 1 FROM sys.partitions p
                  WHERE p.object_id=OBJECT_ID(N'platform.PilotProtocolGate') AND p.hobt_id=g.resource_associated_entity_id)))
            AND EXISTS (SELECT 1 FROM sys.dm_tran_locks waiting
              JOIN winner_locks held ON held.resource_database_id=waiting.resource_database_id
                AND held.resource_type=waiting.resource_type
                AND ISNULL(held.resource_subtype,'')=ISNULL(waiting.resource_subtype,'')
                AND held.resource_associated_entity_id=waiting.resource_associated_entity_id
                AND ISNULL(held.resource_description,'')=ISNULL(waiting.resource_description,'')
                AND held.resource_lock_partition=waiting.resource_lock_partition
              WHERE waiting.request_session_id=r.session_id AND waiting.request_status='WAIT'
                AND waiting.request_mode IN ('S','IS')
                AND ((held.resource_type='OBJECT' AND held.resource_associated_entity_id=OBJECT_ID(N'platform.OpenIddictTokens'))
                  OR (held.resource_type IN ('KEY','PAGE','RID','HOBT') AND EXISTS (SELECT 1 FROM sys.partitions p
                    WHERE p.object_id=OBJECT_ID(N'platform.OpenIddictTokens') AND p.hobt_id=held.resource_associated_entity_id))))
          ));
        """;
}
