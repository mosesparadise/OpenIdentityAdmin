namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Keeps a parent graph alive and suspended until its one actual nested client scope finishes.</summary>
internal sealed class ProtocolStorageChildren
{
    private readonly object sync = new();
    private TaskCompletionSource? pending;
    internal bool IsIdle
    {
        /// <summary>Observes child occupancy atomically with admission and completion.</summary>
        get
        {
            lock (sync)
            {
                return pending is null;
            }
        }
    }

    /// <summary>Admits one child without queueing or allowing sibling native transport.</summary>
    internal IDisposable Enter()
    {
        lock (sync)
        {
            if (pending is not null)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return new Lease(this);
        }
    }

    /// <summary>Retains parent services until actual child disposal completes.</summary>
    internal Task DrainAsync()
    {
        lock (sync)
        {
            return pending?.Task ?? Task.CompletedTask;
        }
    }

    /// <summary>Releases exactly the child admitted by this instance.</summary>
    private void Exit()
    {
        lock (sync)
        {
            var signal = pending;
            pending = null;
            signal!.TrySetResult();
        }
    }

    /// <summary>Owns one release even when disposal is observed more than once.</summary>
    private sealed class Lease(ProtocolStorageChildren owner) : IDisposable
    {
        private int disposed;
        /// <summary>Releases parent occupancy only once.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                owner.Exit();
            }
        }
    }
}
