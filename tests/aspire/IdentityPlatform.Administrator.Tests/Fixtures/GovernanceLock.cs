using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

public sealed class GovernanceLock : IAsyncDisposable
{
    private readonly AsyncServiceScope scope;
    private readonly IDbContextTransaction transaction;
    private readonly int blocker;
    private GovernanceLock(AsyncServiceScope scope, IDbContextTransaction transaction, int blocker)
    {
        this.scope = scope;
        this.transaction = transaction;
        this.blocker = blocker;
    }

    public static async Task<GovernanceLock> AcquireAsync(BootstrapDatabase lease, bool audit = false)
    {
        var scope = lease.Administrator.CreateAsyncScope();
        try
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var transaction = await database.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            try
            {
                if (audit)
                {
                    var sql = database.Database.IsSqlServer()
                        ? "SELECT COUNT(*) FROM [platform].[SecurityAuditIntent] WITH (TABLOCKX, HOLDLOCK)"
                        : "LOCK TABLE platform.\"SecurityAuditIntent\" IN ACCESS EXCLUSIVE MODE";
                    await database.Database.ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken);
                }
                else
                {
                    await database.Set<AdministratorGovernance>().ExecuteUpdateAsync(update => update.SetProperty(value => value.Revision, value => value.Revision + 1), TestContext.Current.CancellationToken);
                }
                await using var identity = database.Database.GetDbConnection().CreateCommand();
                identity.Transaction = transaction.GetDbTransaction();
                identity.CommandText = database.Database.IsSqlServer() ? "SELECT @@SPID" : "SELECT pg_backend_pid()";
                var blocker = Convert.ToInt32(await identity.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
                return new GovernanceLock(scope, transaction, blocker);
            }
            catch
            {
                await transaction.DisposeAsync();
                throw;
            }
        }
        catch
        {
            await scope.DisposeAsync();
            throw;
        }
    }

    public async Task ObserveWaitersAsync(BootstrapDatabase lease, int minimum)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        await using var scope = lease.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var connection = database.Database.GetDbConnection();
        await connection.OpenAsync(deadline.Token);
        while (true)
        {
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 3;
            command.CommandText = database.Database.IsSqlServer()
                ? "WITH blocked AS (SELECT session_id FROM sys.dm_exec_requests WHERE database_id = DB_ID() AND blocking_session_id = @blocker UNION ALL SELECT r.session_id FROM sys.dm_exec_requests r JOIN blocked b ON r.blocking_session_id = b.session_id WHERE r.database_id = DB_ID()) SELECT COUNT(DISTINCT b.session_id) FROM blocked b JOIN sys.dm_exec_sessions s ON s.session_id = b.session_id WHERE s.login_name = 'platform_runtime'"
                : "WITH RECURSIVE blocked(pid) AS (SELECT pid FROM pg_stat_activity WHERE datname = current_database() AND @blocker = ANY(pg_blocking_pids(pid)) UNION SELECT a.pid FROM pg_stat_activity a JOIN blocked b ON b.pid = ANY(pg_blocking_pids(a.pid)) WHERE a.datname = current_database()) SELECT COUNT(*) FROM blocked b JOIN pg_stat_activity a USING(pid) WHERE a.usename = 'platform_runtime'";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "blocker";
            parameter.Value = blocker;
            command.Parameters.Add(parameter);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(deadline.Token), System.Globalization.CultureInfo.InvariantCulture) >= minimum)
            {
                return;
            }
            await Task.Delay(50, deadline.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await transaction.DisposeAsync();
        }
        finally
        {
            await scope.DisposeAsync();
        }
    }
}
