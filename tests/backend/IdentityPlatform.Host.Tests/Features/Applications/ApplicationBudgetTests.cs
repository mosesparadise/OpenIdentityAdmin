using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Host.Tests.Features.Applications;

/// <summary>Checks independent wall, monotonic and caller boundaries without waiting or renewing a management request.</summary>
public sealed class ApplicationBudgetTests
{
    private const int DeadlineSeconds = 5;
    private const int BeforeDeadlineSeconds = 4;

    /// <summary>Neither a frozen wall clock nor a backwards adjustment can extend the original read deadline.</summary>
    [Fact]
    public void Given_OriginalBudget_When_EitherClockReachesDeadlineOrRollsBack_Then_PublicationIsDenied()
    {
        var clock = new Clock();
        using var budget = new ApplicationReadBudget(clock, CancellationToken.None);
        clock.Elapsed = TimeSpan.FromSeconds(BeforeDeadlineSeconds);
        budget.RequireCurrent();
        clock.Elapsed = TimeSpan.FromSeconds(DeadlineSeconds);
        Assert.Throws<OperationCanceledException>(budget.RequireCurrent);
        clock.Elapsed = TimeSpan.Zero;
        clock.Now = clock.Initial.AddSeconds(DeadlineSeconds);
        Assert.Throws<OperationCanceledException>(budget.RequireCurrent);
        clock.Now = clock.Initial.AddTicks(-1);
        Assert.Throws<OperationCanceledException>(budget.RequireCurrent);
    }

    /// <summary>Caller cancellation immediately cancels the same token observed by all nested work.</summary>
    [Fact]
    public void Given_LinkedCaller_When_Canceled_Then_NestedWorkAndPublicationShareCancellation()
    {
        using var caller = new CancellationTokenSource();
        using var budget = new ApplicationReadBudget(new Clock(), caller.Token);
        Assert.False(budget.Token.IsCancellationRequested);
        caller.Cancel();
        Assert.True(budget.Token.IsCancellationRequested);
        Assert.Throws<OperationCanceledException>(budget.RequireCurrent);
    }

    /// <summary>Models independent clock dimensions without scheduling any test-owned asynchronous work.</summary>
    private sealed class Clock : TimeProvider
    {
        internal readonly DateTimeOffset Initial = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
        internal DateTimeOffset? Now
        {
            get; set;
        }
        internal TimeSpan Elapsed
        {
            get; set;
        }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        /// <summary>Returns an independently controlled wall observation.</summary>
        public override DateTimeOffset GetUtcNow() => Now ?? Initial;
        /// <summary>Returns the monotonic elapsed duration in its declared tick frequency.</summary>
        public override long GetTimestamp() => Elapsed.Ticks;
    }
}
