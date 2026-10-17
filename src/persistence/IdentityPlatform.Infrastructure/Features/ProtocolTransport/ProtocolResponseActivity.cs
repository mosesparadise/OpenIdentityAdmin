using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Infrastructure.Features.ProtocolTransport;

/// <summary>Retains asynchronous staging operations until the live HTTP request can release scoped services.</summary>
internal sealed class ProtocolResponseActivity(ProtocolStorageRequest request)
{
    private readonly object sync = new();
    private readonly TaskCompletionSource drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int active;
    private bool closed;

    /// <summary>Admits work synchronously so even an abandoned returned task remains part of the request.</summary>
    internal IDisposable Enter()
    {
        lock (sync)
        {
            if (closed)
            {
                request.Fail();
                throw ProtocolStorageOwner.Unavailable();
            }
            active++;
            return new Lease(this);
        }
    }

    /// <summary>Stops further producers and waits for every actual admitted staging operation.</summary>
    internal Task CloseAsync()
    {
        lock (sync)
        {
            closed = true;
            if (active == 0)
            {
                drained.TrySetResult();
            }
            return drained.Task;
        }
    }

    /// <summary>Signals completion only when the final retained producer has actually returned.</summary>
    private void Exit()
    {
        lock (sync)
        {
            active--;
            if (closed && active == 0)
            {
                drained.TrySetResult();
            }
        }
    }

    /// <summary>Releases each staging admission exactly once.</summary>
    private sealed class Lease(ProtocolResponseActivity activity) : IDisposable
    {
        private int released;
        /// <summary>Joins the retained operation even when its producer throws.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
            {
                activity.Exit();
            }
        }
    }
}
