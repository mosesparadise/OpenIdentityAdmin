using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Owns native execution, reader lifetimes and terminal faults inside an explicitly owned service scope.</summary>
internal sealed class ProtocolStorageOwner(IOpenIddictEntityFrameworkCoreContext native, TimeProvider clock) : IDisposable
{
    private const string UnavailableMessage = "Native protocol storage ownership is unavailable.";
    private readonly object sync = new();
    private readonly HashSet<IAsyncDisposable> readers = [];
    private TaskCompletionSource idle = CompletedSignal();
    private bool executing;
    private bool closing;
    private bool faulted;
    private bool disposed;
    private ProtocolStoragePhase? phase;
    private ProtocolStorageLifetime? request;
    private ProtocolStorageRequest? original;
    internal ProtocolDispatchTracker Dispatches { get; } = new();
    internal CancellationToken RequestToken => request?.Token ?? throw Unavailable();
    private DbContext? database;
    private ProtocolStorageScope? owningScope;
    internal ProtocolStorageTransaction? Transaction
    {
        get; private set;
    }
    internal DbContext Database => database ?? throw Unavailable();
    internal TimeProvider Clock => clock;

    /// <summary>Provides a finite safe ownership error without disclosing native connection or request material.</summary>
    internal static InvalidOperationException Unavailable() => new(UnavailableMessage);

    /// <summary>Activates only for the scope handle that retains the complete native resource graph through cleanup.</summary>
    internal async Task StartAsync(ProtocolStorageScope scope, CancellationToken token, ProtocolStorageRequest? original = null)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (request is not null)
        {
            throw Unavailable();
        }
        owningScope = scope;
        this.original = original;
        if (original is not null && !ReferenceEquals(original.Clock, clock))
        {
            throw Unavailable();
        }
        original?.RequireCurrent();
        request = new(clock, original?.Remaining ?? ProtocolStorageLifetime.RequestLimit, token);
        request.RequireCurrent();
        database = await native.GetDbContextAsync(request.Token);
        original?.RequireCurrent();
        request.RequireCurrent();
        if (Database.Database.CurrentTransaction is not null)
        {
            throw Unavailable();
        }
        phase = new(new(clock, request.Remaining, request.Token));
    }

    /// <summary>Rejects swallowed faults and late dispatch results without reopening a completed transaction phase.</summary>
    internal void RequireHealthy()
    {
        lock (sync)
        {
            if (request is null || faulted || disposed)
            {
                throw Unavailable();
            }
            original?.RequireCurrent();
            request.RequireCurrent();
        }
    }

    /// <summary>Requires all callback-owned native work to finish before accepting its result.</summary>
    internal void RequireCompleted()
    {
        lock (sync)
        {
            RequireHealthy();
            if (executing || readers.Count != 0 || !Dispatches.IsIdle)
            {
                throw Unavailable();
            }
        }
    }

    /// <summary>Admits a native dispatch atomically with storage closure while keeping its lifetime separate from EF execution.</summary>
    internal ProtocolDispatchTracker.Frame BeginDispatch()
    {
        lock (sync)
        {
            Require(phase, false);
            try
            {
                if (original is null || executing)
                {
                    throw Unavailable();
                }
                return Dispatches.Enter();
            }
            catch { Fail(); throw; }
        }
    }

    /// <summary>Captures the original epoch synchronously, before a deferred query can be returned to a manager.</summary>
    internal ProtocolStoragePhase Capture(bool write = false)
    {
        lock (sync)
        {
            Require(phase, write);
            return phase!;
        }
    }

    /// <summary>Requires original time and exact epoch/context/transaction ownership; every rejection latches terminal failure.</summary>
    private void Require(ProtocolStoragePhase? expected, bool write)
    {
        try
        {
            if (request is null || expected is null || !ReferenceEquals(phase, expected) || closing || faulted)
            {
                throw Unavailable();
            }
            original?.RequireCurrent();
            request.RequireCurrent();
            expected.RequireCurrent(Database, write);
        }
        catch { faulted = true; throw; }
    }

    /// <summary>Acquires nonqueueing native execution separately from outstanding reader ownership.</summary>
    internal ProtocolStorageExecution Enter(ProtocolStoragePhase expected, CancellationToken token, bool write = false, bool cleanup = false)
    {
        lock (sync)
        {
            if (!cleanup)
            {
                Require(expected, write);
            }
            if (executing || !ReferenceEquals(phase, expected))
            {
                faulted = true;
                throw Unavailable();
            }
            var execution = new ProtocolStorageExecution(this, expected, cleanup, token);
            executing = true;
            idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return execution;
        }
    }

    /// <summary>Validates completed native work before returning a value, including native completed-task paths.</summary>
    internal void Check(ProtocolStoragePhase expected, CancellationToken token)
    {
        lock (sync)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                Require(expected, false);
            }
            catch { faulted = true; throw; }
        }
    }

    /// <summary>Rejects a context substituted by the installed native wrapper before base-store database access.</summary>
    internal void RequireContext(DbContext actual)
    {
        lock (sync)
        {
            Require(phase, false);
            if (!executing || !ReferenceEquals(Database, actual))
            {
                faulted = true;
                throw Unavailable();
            }
        }
    }

    /// <summary>Records failures before native managers can catch cancellation and return an ordinary false result.</summary>
    internal void Fail()
    {
        lock (sync)
        {
            faulted = true;
            original?.Fail();
        }
    }

    /// <summary>Releases occupancy only after the underlying native operation has actually completed.</summary>
    internal void Exit()
    {
        lock (sync)
        {
            executing = false;
            idle.TrySetResult();
        }
    }

    /// <summary>Serializes cleanup-only operations after cancellation without allowing ordinary work to queue or resume.</summary>
    internal async ValueTask<ProtocolStorageExecution> EnterCleanupAsync(ProtocolStoragePhase expected)
    {
        while (true)
        {
            Task active;
            lock (sync)
            {
                if (!executing)
                {
                    return Enter(expected, CancellationToken.None, cleanup: true);
                }
                active = idle.Task;
            }
            await active;
        }
    }

    /// <summary>Registers an actual enumerator, leaving never-enumerated query sources free of unreleaseable ownership.</summary>
    internal void AddReader(ProtocolStoragePhase expected, IAsyncDisposable reader)
    {
        lock (sync)
        {
            Require(expected, false);
            if (executing)
            {
                faulted = true;
                throw Unavailable();
            }
            readers.Add(reader);
        }
    }

    /// <summary>Releases reader lifetime only after its native disposal has completed.</summary>
    internal void RemoveReader(IAsyncDisposable reader)
    {
        lock (sync)
        {
            readers.Remove(reader);
        }
    }

    /// <summary>Starts a transaction epoch before any acquisition, without resetting the original request budget.</summary>
    internal ProtocolStorageTransaction CreateTransaction()
    {
        lock (sync)
        {
            Require(phase, false);
            if (executing || readers.Count != 0 || Transaction is not null)
            {
                faulted = true;
                throw Unavailable();
            }
            phase!.Lifetime.Dispose();
            var remaining = request!.Remaining;
            var limit = remaining < ProtocolStorageLifetime.TransactionLimit ? remaining : ProtocolStorageLifetime.TransactionLimit;
            phase = new(new(clock, limit, request.Token));
            Transaction = new(this, phase);
            return Transaction;
        }
    }

    /// <summary>Disallows commit while any reader remains outstanding, including readers paused between native yields.</summary>
    internal void RequireNoReaders()
    {
        lock (sync)
        {
            try
            {
                Dispatches.RequireCommitContext();
            }
            catch { Fail(); throw; }
            if (readers.Count != 0)
            {
                faulted = true;
                throw Unavailable();
            }
        }
    }

    /// <summary>Validates a completed commit while allowing EF to clear only the exact transaction it just committed.</summary>
    internal void CompleteCommit(ProtocolStoragePhase expected, CancellationToken token)
    {
        lock (sync)
        {
            token.ThrowIfCancellationRequested();
            original?.RequireCurrent();
            request!.RequireCurrent();
            expected.Lifetime.RequireCurrent();
            var current = Database.Database.CurrentTransaction;
            if (closing || faulted || expected.Closing || !ReferenceEquals(phase, expected) || readers.Count != 0 ||
                (current is not null && !ReferenceEquals(current, expected.Transaction)))
            {
                throw Unavailable();
            }
            Dispatches.RequireCommitContext();
            expected.Committed = true;
        }
    }

    /// <summary>Closes new work first; cancellation does not release occupancy or start concurrent context cleanup.</summary>
    internal void StopAdmission(ProtocolStoragePhase? expected = null)
    {
        lock (sync)
        {
            if (expected is null)
            {
                closing = true;
            }
            else if (!ReferenceEquals(phase, expected))
            {
                throw Unavailable();
            }
            if (phase is not null)
            {
                phase.Closing = true;
            }
        }
    }

    /// <summary>Dispatches cancellation callbacks only after admission and the single cleanup task have been published.</summary>
    internal async Task CloseAsync(ProtocolStoragePhase? expected = null)
    {
        try
        {
            if (expected is null)
            {
                if (request is not null)
                {
                    await request.CancelAsync();
                }
            }
            else
            {
                await expected.Lifetime.CancelAsync();
            }
        }
        catch { Fail(); throw; }
    }

    /// <summary>Drains calls first, then idle readers; waiting for reader lifetime before disposal would deadlock.</summary>
    internal async Task DrainAsync()
    {
        Task active;
        lock (sync)
        {
            active = idle.Task;
        }
        await active;
        IAsyncDisposable[] pending;
        lock (sync)
        {
            pending = [.. readers];
        }
        List<Exception> failures = [];
        foreach (var reader in pending)
        {
            try
            {
                await reader.DisposeAsync();
            }
            catch (Exception error) { Fail(); failures.Add(error); }
        }
        if (failures.Count != 0)
        {
            throw new AggregateException(UnavailableMessage, failures);
        }
    }

    /// <summary>Ends the owning scope after one transaction, preventing stale tracked state from entering a later epoch.</summary>
    internal void EndPhase(ProtocolStoragePhase expected)
    {
        lock (sync)
        {
            if (!ReferenceEquals(phase, expected) || executing || readers.Count != 0)
            {
                faulted = true;
                throw Unavailable();
            }
            expected.Lifetime.Dispose();
            Transaction = null;
            closing = true;
            phase = null;
        }
    }

    /// <summary>Releases lifetime resources only after all native resources and readers have been drained.</summary>
    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }
            if (executing || readers.Count != 0 || Transaction is not null || (request is not null && !closing))
            {
                throw Unavailable();
            }
            disposed = true;
        }
        phase?.Lifetime.Dispose();
        request?.Dispose();
        GC.KeepAlive(owningScope);
    }

    /// <summary>Creates an initially idle signal with asynchronous continuation scheduling.</summary>
    private static TaskCompletionSource CompletedSignal()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult();
        return signal;
    }
}
