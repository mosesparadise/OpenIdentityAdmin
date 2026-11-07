using System.Data.Common;
using System.Globalization;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Distribution;

/// <summary>Holds a native INSERT inside its real issuer transaction and observes exact provider blocking relationships.</summary>
internal sealed class PilotReplayDatabaseBarrier : IAsyncDisposable
{
    private const int ObserveSeconds = 2;
    private const int PollMilliseconds = 10;
    private const int CleanupSeconds = 15;
    private const string AuthorizationParameter = "authorization";
    private const string BlockerParameter = "blocker";
    private const string GateParameter = "gate";
    private const string SessionParameter = "session";
    private const string EarlyCompletion = "An exchange completed before the required database ordering was observed.";
    private const string DistinctSession = "The runtime waiter must own a distinct database session.";
    private const string AmbiguousWaiter = "The isolated barrier has more than one candidate runtime waiter.";
    private const string CleanupFailure = "Ordered pilot barrier cleanup failed.";
    private readonly string provider;
    private readonly int winnerProcess;
    private readonly int replayProcess;
    private readonly AsyncServiceScope owner;
    private readonly AsyncServiceScope observer;
    private readonly PlatformDbContext database;
    private readonly PlatformDbContext observations;
    private IDbContextTransaction? transaction;
    private readonly HashSet<int> sessions = [];
    private int holder;

    /// <summary>Uses independent privileged fixture connections without changing either issuer runtime principal.</summary>
    private PilotReplayDatabaseBarrier(IssuerEnvironment environment, string provider, int winnerProcess, int replayProcess)
    {
        this.provider = provider;
        this.winnerProcess = winnerProcess;
        this.replayProcess = replayProcess;
        owner = environment.Services.CreateAsyncScope();
        observer = environment.Services.CreateAsyncScope();
        database = owner.ServiceProvider.GetRequiredService<PlatformDbContext>();
        observations = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
    }

    /// <summary>Creates the finite barrier before either exchange, preserving native credentials and original expiry.</summary>
    internal static async Task<PilotReplayDatabaseBarrier> CreateAsync(IssuerEnvironment environment, string provider, string authorization,
        int winnerProcess, int replayProcess)
    {
        Assert.True(winnerProcess > 0 && replayProcess > 0 && winnerProcess != replayProcess);
        var barrier = new PilotReplayDatabaseBarrier(environment, provider, winnerProcess, replayProcess);
        try
        {
            await barrier.InitializeAsync(authorization, TestContext.Current.CancellationToken);
            return barrier;
        }
        catch
        {
            try
            {
                await barrier.DisposeAsync();
            }
            catch (Exception) { environment.Invalidate(); }
            throw;
        }
    }

    /// <summary>Installs the test-only trigger and locks only its dedicated row, never a native authority row.</summary>
    private async Task InitializeAsync(string authorization, CancellationToken token)
    {
        await observations.Database.OpenConnectionAsync(token);
        foreach (var sql in PilotReplayBarrierSql.Create(provider))
        {
            await ExecuteAsync(sql, token);
        }
        await using (var target = Command(provider == PilotReplayBarrierSql.PostgreSql ? PilotReplayBarrierSql.Target : PilotReplayBarrierSql.SqlTarget))
        {
            Add(target, AuthorizationParameter, authorization);
            Assert.Equal(1, await target.ExecuteNonQueryAsync(token));
        }
        transaction = await database.Database.BeginTransactionAsync(token);
        await using var held = database.Database.GetDbConnection().CreateCommand();
        held.Transaction = transaction.GetDbTransaction();
        held.CommandTimeout = ObserveSeconds;
        held.CommandText = provider == PilotReplayBarrierSql.PostgreSql ? PilotReplayBarrierSql.PgSession : PilotReplayBarrierSql.SqlSession;
        holder = Convert.ToInt32(await held.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
        held.CommandText = provider == PilotReplayBarrierSql.PostgreSql ? PilotReplayBarrierSql.PgHold : PilotReplayBarrierSql.SqlHold;
        Assert.Equal(1, await held.ExecuteNonQueryAsync(token));
    }

    /// <summary>Finds the sole actual issuer blocked by the private INSERT barrier, not by arrival timing.</summary>
    internal Task<int> WaitForWinnerAsync(Task<HttpResponseMessage> winner, CancellationToken token) =>
        WaitAsync(holder, gate: false, [winner], token);

    /// <summary>Requires a peer process blocked by the winner's gate or an exact native read lock in its still-gated generation transaction.</summary>
    internal Task<int> WaitForReplayAsync(int winner, Task<HttpResponseMessage> issuance, Task<HttpResponseMessage> replay, CancellationToken token) =>
        WaitAsync(winner, gate: true, [issuance, replay], token);

    /// <summary>Propagates early HTTP or query failure; a timeout cannot masquerade as successful lock evidence.</summary>
    private async Task<int> WaitAsync(int blocker, bool gate, Task<HttpResponseMessage>[] pending, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(ObserveSeconds));
        var diagnostics = new PilotReplaySqlWaitDiagnostics();
        try
        {
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                foreach (var request in pending.Where(value => value.IsCompleted))
                {
                    var response = await request;
                    Assert.Fail(EarlyCompletion + (int)response.StatusCode);
                }
                var session = await ReadWaiterAsync(blocker, gate, diagnostics, deadline.Token);
                deadline.Token.ThrowIfCancellationRequested();
                if (session is { } observed)
                {
                    Assert.True(observed != blocker && observed != holder, DistinctSession);
                    sessions.Add(observed);
                    return observed;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(PollMilliseconds), deadline.Token);
            }
        }
        catch
        {
            if (provider != PilotReplayBarrierSql.PostgreSql)
            {
                diagnostics.Report(gate);
            }
            throw;
        }
    }

    /// <summary>Correlates direct provider blocking, database, runtime identity and gate or exact native read resource ownership.</summary>
    private async Task<int?> ReadWaiterAsync(int blocker, bool gate, PilotReplaySqlWaitDiagnostics diagnostics, CancellationToken token)
    {
        var postgres = provider == PilotReplayBarrierSql.PostgreSql;
        await using var command = Command(postgres ? PilotReplayBarrierSql.PgWait : PilotReplaySqlWait.Query + PilotReplaySqlWaitDiagnostics.Query);
        Add(command, BlockerParameter, blocker);
        Add(command, GateParameter, gate ? 1 : 0);
        if (!postgres)
        {
            Add(command, PilotReplaySqlWaitDiagnostics.HolderParameter, holder);
            Add(command, PilotReplaySqlWait.WinnerProcessParameter, winnerProcess);
            Add(command, PilotReplaySqlWait.ReplayProcessParameter, replayProcess);
        }
        await using var rows = await command.ExecuteReaderAsync(token);
        int? session = null;
        PilotReplaySqlWaitMode? mode = null;
        if (await rows.ReadAsync(token))
        {
            session = rows.GetInt32(0);
            if (!postgres)
            {
                mode = (PilotReplaySqlWaitMode)rows.GetInt32(1);
                Assert.True(Enum.IsDefined(mode.Value));
            }
            Assert.False(await rows.ReadAsync(token), AmbiguousWaiter);
        }
        if (!postgres)
        {
            await diagnostics.CaptureAsync(rows, token);
        }
        if (mode is { } observed)
        {
            TestContext.Current.TestOutputHelper?.WriteLine(PilotReplaySqlWait.ObservedMode, observed);
        }
        return session;
    }

    /// <summary>Releases the fixture transaction without cancelling either genuine exchange.</summary>
    internal async Task ReleaseAsync(CancellationToken token)
    {
        if (transaction is { } held)
        {
            await held.RollbackAsync(token);
            await held.DisposeAsync();
            transaction = null;
        }
    }

    /// <summary>Proves captured runtime sessions no longer execute or retain transactions before support objects disappear.</summary>
    internal async Task DrainAsync(CancellationToken token)
    {
        foreach (var session in sessions)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                await using var command = Command(provider == PilotReplayBarrierSql.PostgreSql ? PilotReplayBarrierSql.PgDrain : PilotReplayBarrierSql.SqlDrain);
                Add(command, SessionParameter, session);
                if (Convert.ToInt64(await command.ExecuteScalarAsync(token), CultureInfo.InvariantCulture) == 0)
                {
                    break;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(PollMilliseconds), token);
            }
        }
    }

    /// <summary>Creates a bounded administrative observer command without logging SQL parameters.</summary>
    private DbCommand Command(string sql)
    {
        var command = observations.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = ObserveSeconds;
        return command;
    }

    /// <summary>Runs fixture DDL independently from the held transaction.</summary>
    private async Task ExecuteAsync(string sql, CancellationToken token)
    {
        await using var command = Command(sql);
        await command.ExecuteNonQueryAsync(token);
    }

    /// <summary>Keeps native identifiers in bound parameters rather than generated SQL or diagnostics.</summary>
    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>Attempts release, trigger removal and both scope disposals even after partial construction.</summary>
    public async ValueTask DisposeAsync()
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
        var failures = new List<Exception>();
        try
        {
            await ReleaseAsync(cleanup.Token);
        }
        catch (Exception error) { failures.Add(error); }
        try
        {
            await ExecuteAsync(PilotReplayBarrierSql.Drop(provider), cleanup.Token);
        }
        catch (Exception error) { failures.Add(error); }
        try
        {
            await owner.DisposeAsync();
        }
        catch (Exception error) { failures.Add(error); }
        try
        {
            await observer.DisposeAsync();
        }
        catch (Exception error) { failures.Add(error); }
        if (failures.Count != 0)
        {
            throw new AggregateException(CleanupFailure, failures);
        }
    }
}
