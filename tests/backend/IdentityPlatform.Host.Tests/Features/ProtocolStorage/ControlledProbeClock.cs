namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Keeps timer callbacks and monotonic time on one explicit timeline for precise boundary controls.</summary>
internal sealed class ControlledProbeClock : StorageProbe.ProbeClock
{
    private readonly List<ControlledTimer> timers = [];

    /// <summary>Registers one-shot timers whose due times advance only with the test's explicit clock.</summary>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ControlledTimer(this, callback, state);
        timer.Change(dueTime, period);
        timers.Add(timer);
        return timer;
    }

    /// <summary>Advances both elapsed-time checks and due callbacks without any dependency on runner scheduling.</summary>
    internal override void Advance(TimeSpan elapsed)
    {
        base.Advance(elapsed);
        foreach (var timer in timers.ToArray())
        {
            timer.Fire();
        }
    }

    /// <summary>Implements the one-shot timer surface used by request cancellation and bounded cleanup.</summary>
    private sealed class ControlledTimer(ControlledProbeClock clock, TimerCallback callback, object? state) : ITimer
    {
        private long due = long.MaxValue;
        private bool disposed;

        /// <summary>Reschedules against the same controlled timestamp and rejects unsupported periodic fixtures.</summary>
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (disposed)
            {
                return false;
            }
            if (period != Timeout.InfiniteTimeSpan || (dueTime < TimeSpan.Zero && dueTime != Timeout.InfiniteTimeSpan))
            {
                throw new ArgumentOutOfRangeException(nameof(period));
            }
            due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : checked(clock.GetTimestamp() + dueTime.Ticks);
            return true;
        }

        /// <summary>Executes each due callback once on explicit advancement, never because wall time elapsed.</summary>
        internal void Fire()
        {
            if (!disposed && due <= clock.GetTimestamp())
            {
                due = long.MaxValue;
                callback(state);
            }
        }

        /// <summary>Prevents any later clock advancement from running a disposed lifetime callback.</summary>
        public void Dispose() => disposed = true;

        /// <summary>Completes synchronously because controlled callbacks only execute during explicit advancement.</summary>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
