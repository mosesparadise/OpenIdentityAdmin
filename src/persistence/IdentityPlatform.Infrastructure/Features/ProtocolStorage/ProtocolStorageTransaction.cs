using System.Data;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Owns one explicit transaction phase, including acquisition time and exactly-once cleanup after actual native completion.</summary>
internal sealed class ProtocolStorageTransaction(ProtocolStorageOwner owner, ProtocolStoragePhase phase) : IAsyncDisposable
{
    private const string CleanupMessage = "Native protocol storage cleanup failed.";
    private readonly object sync = new();
    private CeremonyConnection? connection;
    private IDbContextTransaction? transaction;
    private Task? cleanup;

    /// <summary>Starts the epoch before connection or transaction acquisition without resetting the request deadline.</summary>
    internal static async Task<ProtocolStorageTransaction> OpenAsync(ProtocolStorageOwner owner)
    {
        var result = owner.CreateTransaction();
        try
        {
            await result.AcquireAsync();
            return result;
        }
        catch
        {
            owner.Fail();
            try
            {
                await result.DisposeAsync();
            }
            catch { owner.Fail(); }
            throw;
        }
    }

    /// <summary>Bounds acquisition with the phase token and keeps partial resources owned until safe disposal.</summary>
    private async Task AcquireAsync()
    {
        using var execution = owner.Enter(phase, CancellationToken.None);
        execution.RequireCurrent();
        connection = new(owner.Database);
        await connection.OpenAsync(execution.Token);
        transaction = await owner.Database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, execution.Token);
        phase.Transaction = transaction;
        execution.RequireCurrent();
    }

    /// <summary>Commits only clean quiescent ownership and rejects late or ambiguous completion without an automatic retry.</summary>
    internal async Task CommitAsync(CancellationToken token = default)
    {
        try
        {
            using var execution = owner.Enter(phase, token, write: true);
            owner.RequireNoReaders();
            execution.RequireCurrent();
            phase.CommitAttempted = true;
            await transaction!.CommitAsync(execution.Token);
            owner.CompleteCommit(phase, execution.Token);
        }
        catch { owner.Fail(); throw; }
    }

    /// <summary>Bounds only waiting for the one real cleanup task; a timeout never permits transaction or context reuse.</summary>
    public async ValueTask DisposeAsync()
    {
        var task = CleanupAsync();
        try
        {
            await task.WaitAsync(ProtocolStorageLifetime.CleanupLimit, owner.Clock);
        }
        catch (TimeoutException)
        {
            owner.Fail();
            ProtocolStorageQuarantine.Retain(task);
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Allows the containing scope to join exactly the same cleanup instead of launching a second rollback.</summary>
    internal Task CleanupAsync()
    {
        lock (sync)
        {
            if (cleanup is null)
            {
                owner.StopAdmission(phase);
                cleanup = CleanupCoreAsync();
            }
            return cleanup;
        }
    }

    /// <summary>Stops admission, drains calls/readers, then restores native resources before ending the epoch.</summary>
    private async Task CleanupCoreAsync()
    {
        await Task.Yield();
        List<Exception> failures = [];
        try
        {
            await owner.CloseAsync(phase);
        }
        catch (Exception error) { failures.Add(error); }
        try
        {
            await owner.DrainAsync();
        }
        catch (Exception error) { failures.Add(error); }
        try
        {
            await DisposeResourcesAsync();
        }
        catch (Exception error) { owner.Fail(); failures.Add(error); }
        finally { owner.EndPhase(phase); }
        if (failures.Count != 0)
        {
            throw new AggregateException(CleanupMessage, failures);
        }
        phase.CleanupCompleted = true;
    }
    /// <summary>Records definite rollback only before any commit attempt; disposal alone never proves the database outcome.</summary>
    private async Task DisposeResourcesAsync()
    {
        using var execution = await owner.EnterCleanupAsync(phase);
        try
        {
            if (transaction is not null)
            {
                try
                {
                    if (!phase.CommitAttempted)
                    {
                        if (!ReferenceEquals(owner.Database.Database.CurrentTransaction, transaction))
                        {
                            throw ProtocolStorageOwner.Unavailable();
                        }
                        await transaction.RollbackAsync(execution.Token);
                        phase.RolledBack = true;
                    }
                }
                finally { await transaction.DisposeAsync(); }
            }
        }
        finally { if (connection is not null) { await connection.DisposeAsync(); } }
    }

}
