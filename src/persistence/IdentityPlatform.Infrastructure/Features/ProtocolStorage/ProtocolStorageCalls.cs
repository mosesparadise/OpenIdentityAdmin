namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Forwards native calls without replacing manager behavior or racing a detached timeout against active EF work.</summary>
internal static class ProtocolStorageCalls
{
    private const string PruneMessage = "Native protocol storage pruning requires bounded cleanup.";
    /// <summary>Captures the epoch before forwarding a value-returning native operation.</summary>
    internal static ValueTask<T> RunAsync<T>(this ProtocolStorageOwner owner, Func<CancellationToken, ValueTask<T>> action,
        CancellationToken token, bool write = false) => RunAsync(owner, owner.Capture(write), action, token, write);

    /// <summary>Waits for real native completion and latches all faults before a manager can swallow them.</summary>
    internal static async ValueTask<T> RunAsync<T>(this ProtocolStorageOwner owner, ProtocolStoragePhase phase,
        Func<CancellationToken, ValueTask<T>> action, CancellationToken token, bool write = false)
    {
        try
        {
            using var execution = owner.Enter(phase, token, write);
            execution.RequireCurrent();
            var result = await action(execution.Token);
            execution.RequireCurrent();
            return result;
        }
        catch { owner.Fail(); throw; }
    }

    /// <summary>Forwards native mutations under the same exact explicit transaction and cancellation checks.</summary>
    internal static async ValueTask RunAsync(this ProtocolStorageOwner owner, Func<CancellationToken, ValueTask> action,
        CancellationToken token, bool write = true)
    {
        await owner.RunAsync(async cancellation => { await action(cancellation); return true; }, token, write);
    }

    /// <summary>Rejects unbounded maintenance and prevents a caller from committing after catching that rejection.</summary>
    internal static ValueTask<long> RejectPrune(this ProtocolStorageOwner owner)
    {
        owner.Fail();
        return ValueTask.FromException<long>(new NotSupportedException(PruneMessage));
    }

    /// <summary>Captures a deferred source now, while acquiring reader ownership only when it is actually enumerated.</summary>
    internal static IAsyncEnumerable<T> Enumerate<T>(this ProtocolStorageOwner owner,
        Func<CancellationToken, IAsyncEnumerable<T>> source, CancellationToken token) =>
        new ProtocolStorageEnumerable<T>(owner, owner.Capture(), source, token);
}
