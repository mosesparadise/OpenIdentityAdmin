namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Distribution;

/// <summary>Defines disposable database-only observation objects; no product handler or endpoint is replaced.</summary>
internal static class PilotReplayBarrierSql
{
    internal const string PostgreSql = "PostgreSql";
    internal const string Target = "INSERT INTO platform.\"PilotReplayTestTarget\" (\"AuthorizationId\") VALUES (@authorization)";
    internal const string SqlTarget = "INSERT INTO [platform].[PilotReplayTestTarget] ([AuthorizationId]) VALUES (@authorization)";
    internal const string PgHold = "UPDATE platform.\"PilotReplayTestBarrier\" SET \"Touch\"=\"Touch\" WHERE \"Id\"=1";
    internal const string SqlHold = "UPDATE [platform].[PilotReplayTestBarrier] WITH (ROWLOCK) SET [Touch]=[Touch] WHERE [Id]=1";
    internal const string PgWait = "SELECT pid FROM pg_stat_activity WHERE datname=current_database() AND usename='platform_runtime' AND state='active' AND @blocker=ANY(pg_blocking_pids(pid)) AND (@gate=0 OR query LIKE '%PilotProtocolGate%')";
    // The trigger executes as owner; retain the authenticated runtime identity across that context switch.
    internal const string SqlWait = "SELECT CAST(r.session_id AS int) FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON s.session_id=r.session_id CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t WHERE r.database_id=DB_ID() AND s.original_login_name='platform_runtime' AND r.blocking_session_id=@blocker AND r.wait_type LIKE 'LCK_M_%' AND (@gate=0 OR t.text LIKE '%PilotProtocolGate%')";
    internal const string PgDrain = "SELECT COUNT(*) FROM pg_stat_activity WHERE pid=@session AND (state<>'idle' OR xact_start IS NOT NULL)";
    internal const string SqlDrain = "SELECT COUNT(*) FROM sys.dm_exec_sessions s WHERE s.session_id=@session AND (s.open_transaction_count>0 OR EXISTS (SELECT 1 FROM sys.dm_exec_requests r WHERE r.session_id=s.session_id) OR EXISTS (SELECT 1 FROM sys.dm_tran_session_transactions t WHERE t.session_id=s.session_id))";
    internal const string PgSession = "SELECT pg_backend_pid()";
    internal const string SqlSession = "SELECT @@SPID";
    private const string PgCreateTables = "CREATE TABLE platform.\"PilotReplayTestTarget\" (\"AuthorizationId\" text NOT NULL); CREATE TABLE platform.\"PilotReplayTestBarrier\" (\"Id\" integer PRIMARY KEY, \"Touch\" integer NOT NULL); INSERT INTO platform.\"PilotReplayTestBarrier\" VALUES (1,0)";
    private const string PgCreateTrigger = "CREATE TRIGGER \"PilotReplayTestInsert\" AFTER INSERT ON platform.\"OpenIddictTokens\" FOR EACH ROW EXECUTE FUNCTION platform.\"PilotReplayTestInsert\"()";
    private const string SqlCreateTables = "CREATE TABLE [platform].[PilotReplayTestTarget] ([AuthorizationId] nvarchar(450) NOT NULL); CREATE TABLE [platform].[PilotReplayTestBarrier] ([Id] int PRIMARY KEY, [Touch] int NOT NULL); INSERT INTO [platform].[PilotReplayTestBarrier] VALUES (1,0)";
    private const string PgDrop = "DROP TRIGGER IF EXISTS \"PilotReplayTestInsert\" ON platform.\"OpenIddictTokens\"; DROP FUNCTION IF EXISTS platform.\"PilotReplayTestInsert\"(); DROP TABLE IF EXISTS platform.\"PilotReplayTestTarget\"; DROP TABLE IF EXISTS platform.\"PilotReplayTestBarrier\"";
    private const string SqlDrop = "DROP TRIGGER IF EXISTS [platform].[PilotReplayTestInsert]; DROP TABLE IF EXISTS [platform].[PilotReplayTestTarget]; DROP TABLE IF EXISTS [platform].[PilotReplayTestBarrier]";

    private const string PgCreateFunction = """
        CREATE FUNCTION platform."PilotReplayTestInsert"() RETURNS trigger
        LANGUAGE plpgsql SECURITY DEFINER SET search_path=pg_catalog AS $$
        BEGIN
          IF NEW."Type"='urn:ietf:params:oauth:token-type:access_token' AND EXISTS
            (SELECT 1 FROM platform."PilotReplayTestTarget" WHERE "AuthorizationId"=NEW."AuthorizationId") THEN
            UPDATE platform."PilotReplayTestBarrier" SET "Touch"="Touch" WHERE "Id"=1;
          END IF;
          RETURN NEW;
        END; $$
        """;
    private const string SqlCreateTrigger = """
        CREATE TRIGGER [platform].[PilotReplayTestInsert] ON [platform].[OpenIddictTokens]
        WITH EXECUTE AS OWNER AFTER INSERT AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS (SELECT 1 FROM inserted i JOIN [platform].[PilotReplayTestTarget] t
            ON t.[AuthorizationId]=i.[AuthorizationId] WHERE i.[Type]='urn:ietf:params:oauth:token-type:access_token')
            UPDATE [platform].[PilotReplayTestBarrier] WITH (ROWLOCK) SET [Touch]=[Touch] WHERE [Id]=1;
        END
        """;

    /// <summary>Targets one authorization without locking its selector row or granting the runtime extra fixture privileges.</summary>
    internal static string[] Create(string provider) => provider == PostgreSql ?
        [PgCreateTables, PgCreateFunction, PgCreateTrigger] : [SqlCreateTables, SqlCreateTrigger];

    /// <summary>Removes trigger execution before removing its private support objects, including partial setup.</summary>
    internal static string Drop(string provider) => provider == PostgreSql ? PgDrop : SqlDrop;
}
