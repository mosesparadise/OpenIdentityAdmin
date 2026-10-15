using IdentityPlatform.Administrator.Tests.Features.Browser;
using IdentityPlatform.Administrator.Tests.Features.Factors;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Seeds hung reads, cancellation and cleanup failures without launching a browser or database.</summary>
public sealed class HistoryDocumentReadTests
{
    private static readonly TimeSpan WorkBudget = TimeSpan.FromSeconds(1);

    /// <summary>A completed task cannot win over cancellation or an exhausted absolute work budget.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_AlreadyCompletedRead_When_CancelledOrExpiredDuringLaunch_Then_ResultIsRejected(string provider)
    {
        Assert.False(string.IsNullOrEmpty(provider));
        using var cancellation = new CancellationTokenSource();
        var clock = new ReadClock();
        // Completes synchronously after revoking the work token.
        Task<int> Cancel()
        {
            cancellation.Cancel();
            return Task.FromResult(1);
        }
        // Completes synchronously at the exact expiration boundary.
        Task<int> Expire()
        {
            clock.Advance(WorkBudget);
            return Task.FromResult(1);
        }
        // A completed task has no pending work to close.
        Task Close() => throw new InvalidOperationException();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HistoryDocumentRead.RunAsync(Cancel, WorkBudget, Close, cancellation.Token));
        await Assert.ThrowsAsync<TimeoutException>(() => HistoryDocumentRead.RunAsync(Expire, WorkBudget, Close, CancellationToken.None, clock));
    }

    /// <summary>Expired and cancelled work cannot start; a completed read needs no teardown.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ExpiredOrCompletedRead_When_Bounded_Then_LaunchAndCleanupOwnershipAreExplicit(string provider)
    {
        Assert.False(string.IsNullOrEmpty(provider));
        var calls = 0;
        // Records whether the bounded coordinator actually launched the read.
        Task<int> Read()
        {
            calls++;
            return Task.FromResult(1);
        }
        // Models the owning page close and completes or rejects its pending read.
        Task Close() => throw new InvalidOperationException();
        await Assert.ThrowsAsync<TimeoutException>(() => HistoryDocumentRead.RunAsync(Read, TimeSpan.Zero, Close, CancellationToken.None));
        Assert.Equal(0, calls);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HistoryDocumentRead.RunAsync(Read, WorkBudget, Close, new(true)));
        Assert.Equal(0, calls);
        Assert.Equal(1, await HistoryDocumentRead.RunAsync(Read, WorkBudget, Close, CancellationToken.None));
        Assert.Equal(1, calls);
    }

    /// <summary>A cancelled in-flight read must close and finish before the cancellation escapes.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_PendingRead_When_Cancelled_Then_PageCloseDrainsItsTaskBeforeReturn(string provider)
    {
        Assert.False(string.IsNullOrEmpty(provider));
        using var cancellation = new CancellationTokenSource();
        var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = false;
        // Records whether the bounded coordinator actually launched the read.
        Task<int> Read()
        {
            cancellation.Cancel();
            return pending.Task;
        }
        // Models the owning page close and completes or rejects its pending read.
        Task Close()
        {
            closed = true;
            pending.SetException(new InvalidOperationException());
            return Task.CompletedTask;
        }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HistoryDocumentRead.RunAsync(Read, WorkBudget, Close, cancellation.Token));
        Assert.True(closed);
        Assert.True(pending.Task.IsCompleted);
    }

    /// <summary>A timed-out read cannot survive owned cleanup, and failed cleanup cannot be treated as a normal timeout.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_HungRead_When_DeadlineExpires_Then_CleanupCompletesOrExplicitlyFails(string provider)
    {
        Assert.False(string.IsNullOrEmpty(provider));
        foreach (var failClose in new[] { false, true })
        {
            var pending = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var closed = false;
            // Models the owning page close and completes or rejects its pending read.
            Task Close()
            {
                closed = true;
                pending.SetException(new InvalidOperationException());
                return failClose ? Task.FromException(new InvalidOperationException()) : Task.CompletedTask;
            }
            var error = await Record.ExceptionAsync(() => HistoryDocumentRead.RunAsync(() => pending.Task,
                TimeSpan.FromMilliseconds(1), Close, CancellationToken.None));
            if (failClose)
            {
                Assert.IsType<HistoryDocumentCleanupException>(error);
            }
            else
            {
                Assert.IsType<TimeoutException>(error);
            }
            Assert.True(closed);
            Assert.True(pending.Task.IsCompleted);
        }
    }

    private sealed class ReadClock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        /// <summary>Returns a deterministic monotonic tick without wall-clock waiting.</summary>
        public override long GetTimestamp() => timestamp;

        /// <summary>Moves exactly to the specified work-budget boundary.</summary>
        internal void Advance(TimeSpan duration) => timestamp += duration.Ticks;
    }
}
