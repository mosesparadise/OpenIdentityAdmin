using System.Data.Common;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

// Independent provider-observed row locks for native redemption and application admission tests.
public sealed class DatabaseRowLock : IAsyncDisposable
{
    private readonly IssuerEnvironment environment;
    private readonly string provider;
    private readonly AsyncServiceScope owner;
    private readonly AsyncServiceScope observer;
    private readonly PlatformDbContext database;
    private readonly PlatformDbContext observations;
    private IDbContextTransaction? transaction;
    private int blocker;

    private DatabaseRowLock(IssuerEnvironment environment, string provider)
    {
        this.environment = environment;
        this.provider = provider;
        owner = environment.Services.CreateAsyncScope();
        observer = environment.Services.CreateAsyncScope();
        database = owner.ServiceProvider.GetRequiredService<PlatformDbContext>();
        observations = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        database.Database.SetCommandTimeout(5);
        observations.Database.SetCommandTimeout(5);
    }

    public sealed record BlockedCommand(int Session, bool IsUpdate);

    public static Task<DatabaseRowLock> AcquireNativeCodeAsync(IssuerEnvironment environment, string provider, string codeId) =>
        AcquireAsync(environment, provider, codeId, budget: false);

    public static Task<DatabaseRowLock> AcquireBudgetAsync(IssuerEnvironment environment, string provider, string budgetId) =>
        AcquireAsync(environment, provider, budgetId, budget: true);

    private static async Task<DatabaseRowLock> AcquireAsync(IssuerEnvironment environment, string provider, string rowId, bool budget)
    {
        var held = new DatabaseRowLock(environment, provider);
        try
        {
            held.transaction = await held.database.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await using (var command = held.database.Database.GetDbConnection().CreateCommand())
            {
                command.Transaction = held.transaction.GetDbTransaction();
                command.CommandTimeout = 5;
                command.CommandText = provider == "PostgreSql" ? "SELECT pg_backend_pid()" : "SELECT @@SPID";
                held.blocker = Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
                command.CommandText = budget ? provider == "PostgreSql"
                    ? "UPDATE platform.\"LoginBrowserBudget\" SET \"LastAdmissionAt\"=\"LastAdmissionAt\" WHERE \"Id\"=@id"
                    : "UPDATE [platform].[LoginBrowserBudget] SET [LastAdmissionAt]=[LastAdmissionAt] WHERE [Id]=@id"
                    : provider == "PostgreSql" ?
                    "UPDATE platform.\"OpenIddictTokens\" SET \"ConcurrencyToken\"=\"ConcurrencyToken\" WHERE \"Id\"=@id" :
                    "UPDATE [platform].[OpenIddictTokens] SET [ConcurrencyToken]=[ConcurrencyToken] WHERE [Id]=@id";
                AddParameter(command, "id", rowId);
                Assert.Equal(1, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
            }
            await held.observations.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
            return held;
        }
        catch
        {
            await held.DisposeAsync();
            throw;
        }
    }

    public async Task<BlockedCommand> WaitForRuntimeAsync(CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(4));
        while (true)
        {
            await using var command = observations.Database.GetDbConnection().CreateCommand();
            command.CommandTimeout = 5;
            command.CommandText = provider == "PostgreSql" ?
                "SELECT pid, CASE WHEN ltrim(query) LIKE 'UPDATE%' THEN 1 ELSE 0 END FROM pg_stat_activity WHERE usename='platform_runtime' AND @blocker = ANY(pg_blocking_pids(pid))" :
                "SELECT CAST(r.session_id AS int), CASE WHEN r.command='UPDATE' THEN 1 ELSE 0 END FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON r.session_id=s.session_id WHERE r.blocking_session_id=@blocker AND s.login_name='platform_runtime'";
            AddParameter(command, "blocker", blocker);
            await using var reader = await command.ExecuteReaderAsync(deadline.Token);
            if (await reader.ReadAsync(deadline.Token))
            {
                return new(reader.GetInt32(0), reader.GetInt32(1) == 1);
            }
            await Task.Delay(25, deadline.Token);
        }
    }

    public async Task DrainAsync(BlockedCommand blocked, CancellationToken cancellationToken)
    {
        // A pooled idle connection may remain, but no request/transaction may still be executing.
        while (true)
        {
            await using var command = observations.Database.GetDbConnection().CreateCommand();
            command.CommandTimeout = 5;
            command.CommandText = provider == "PostgreSql" ?
                "SELECT count(*) FROM pg_stat_activity WHERE pid=@session AND state <> 'idle'" :
                "SELECT count(*) FROM sys.dm_exec_sessions s WHERE s.session_id=@session AND (s.open_transaction_count > 0 OR EXISTS (SELECT 1 FROM sys.dm_exec_requests r WHERE r.session_id=s.session_id) OR EXISTS (SELECT 1 FROM sys.dm_tran_session_transactions t WHERE t.session_id=s.session_id))";
            AddParameter(command, "session", blocked.Session);
            if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) == 0)
            {
                return;
            }
            await Task.Delay(25, cancellationToken);
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    public async Task ReleaseAsync()
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cleanup.Token);
                await transaction.DisposeAsync();
                transaction = null;
            }
        }
        catch (Exception) { environment.Invalidate(); }
        Assert.False(environment.Invalidated, "Database row lock release failed.");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await ReleaseAsync();
        }
        finally
        {
            try
            {
                await owner.DisposeAsync();
            }
            finally { await observer.DisposeAsync(); }
        }
    }
}
