namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Tracks actual dispatch completion separately from EF occupancy and rejects overlapping sibling dispatch chains.</summary>
internal sealed class ProtocolDispatchTracker
{
    private readonly object sync = new();
    private readonly AsyncLocal<Frame?> current = new();
    private Frame? leaf;
    private int count;
    private TaskCompletionSource? idle;
    internal bool IsCurrentFlow => current.Value is { Completed: false };
    internal bool IsIdle
    {
        /// <summary>Observes quiescence while holding the same lock as dispatch admission and completion.</summary>
        get
        {
            lock (sync)
            {
                return count == 0;
            }
        }
    }

    /// <summary>Admits an awaited nested child only from the active leaf; a sibling cannot race transaction token replacement.</summary>
    internal Frame Enter()
    {
        lock (sync)
        {
            if (!ReferenceEquals(current.Value, leaf))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            if (count++ == 0)
            {
                idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            var frame = new Frame(current.Value);
            current.Value = leaf = frame;
            return frame;
        }
    }

    /// <summary>Releases the actual completed dispatch; an unawaited nested child makes the surrounding flow invalid.</summary>
    internal bool Exit(Frame frame)
    {
        lock (sync)
        {
            var ordered = ReferenceEquals(leaf, frame);
            frame.Completed = true;
            if (ordered)
            {
                do
                {
                    leaf = leaf?.Parent;
                } while (leaf is { Completed: true });
            }
            current.Value = frame.Parent;
            if (--count == 0)
            {
                idle!.TrySetResult();
            }
            return ordered;
        }
    }

    /// <summary>Allows commit only outside dispatch or from its current active leaf, never above pending native work.</summary>
    internal void RequireCommitContext()
    {
        lock (sync)
        {
            if (!ReferenceEquals(current.Value, leaf))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
        }
    }

    /// <summary>Waits for real dispatcher completion before the owning child scope can dispose its services.</summary>
    internal Task DrainAsync()
    {
        lock (sync)
        {
            return idle?.Task ?? Task.CompletedTask;
        }
    }

    /// <summary>Identifies one nested dispatch without storing request or credential material.</summary>
    internal sealed class Frame(Frame? parent)
    {
        internal Frame? Parent => parent;
        internal bool Completed
        {
            get; set;
        }
    }
}
