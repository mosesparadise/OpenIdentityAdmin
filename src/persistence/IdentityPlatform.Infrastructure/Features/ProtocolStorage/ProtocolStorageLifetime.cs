namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Keeps cancellation timers and strict monotonic bounds on the same original lifetime.</summary>
internal sealed class ProtocolStorageLifetime : IDisposable
{
    internal static readonly TimeSpan RequestLimit = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan TransactionLimit = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan CleanupLimit = TimeSpan.FromSeconds(5);
    private readonly TimeProvider clock;
    private readonly long started;
    private readonly TimeSpan limit;
    private readonly CancellationTokenSource timer;
    private readonly CancellationTokenSource linked;
    internal CancellationToken Token => linked.Token;
    internal TimeSpan Remaining
    {
        /// <summary>Computes the strict remaining monotonic budget once per observation.</summary>
        get
        {
            var remaining = limit - clock.GetElapsedTime(started);
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>Starts the deadline before acquisition and links cancellation without restarting a parent budget.</summary>
    internal ProtocolStorageLifetime(TimeProvider clock, TimeSpan limit, CancellationToken parent)
    {
        this.clock = clock;
        this.limit = limit;
        started = clock.GetTimestamp();
        timer = new CancellationTokenSource(limit, clock);
        linked = CancellationTokenSource.CreateLinkedTokenSource(parent, timer.Token);
    }

    /// <summary>Rejects exact expiry even when timer callbacks or already-completed native tasks run late.</summary>
    internal void RequireCurrent()
    {
        Token.ThrowIfCancellationRequested();
        if (Remaining <= TimeSpan.Zero)
        {
            throw new OperationCanceledException(Token);
        }
    }

    /// <summary>Requests cancellation while leaving lifetime resources alive until native work drains.</summary>
    internal Task CancelAsync() => linked.CancelAsync();

    /// <summary>Releases timers only after the owning scope has drained all users.</summary>
    public void Dispose()
    {
        linked.Dispose();
        timer.Dispose();
    }
}
