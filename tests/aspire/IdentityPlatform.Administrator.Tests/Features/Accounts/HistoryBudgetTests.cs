using IdentityPlatform.Administrator.Tests.Features.Browser;
using IdentityPlatform.Administrator.Tests.Features.Factors;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Proves diagnostic coordination cannot turn expired, failed or unobserved delivery into acceptance.</summary>
public sealed class HistoryBudgetTests
{
    private const string Canary = "synthetic-private-delivery-error";
    private const string InvalidMode = "unknown-experiment-mode";
    private static readonly TimeSpan DeliveryCost = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan ExpiredCost = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan TestDeadline = TimeSpan.FromSeconds(2);

    /// <summary>Delivery consumes the same deadline as inspection; exhausted budgets never reach a zero-timeout browser call.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_DeliveryConsumesBudget_When_InspectedOrExpired_Then_OnlyRemainingTimeIsAllowed(string provider)
    {
        Assert.False(string.IsNullOrWhiteSpace(provider));
        var clock = new ProbeClock();
        var budget = new HistoryObservationBudget(clock);
        clock.Advance(DeliveryCost);
        float observed = 0;
        await budget.ObserveAsync(Task.CompletedTask, timeout => { observed = timeout; return Task.CompletedTask; }, TestContext.Current.CancellationToken);
        Assert.Equal(1000, observed);
        clock.Advance(ExpiredCost);
        var called = false;
        await Assert.ThrowsAsync<TimeoutException>(() => budget.ObserveAsync(Task.CompletedTask,
            _ => { called = true; return Task.CompletedTask; }, TestContext.Current.CancellationToken));
        Assert.False(called);
    }

    /// <summary>Failed or incomplete fulfillment cannot invoke the safe-document oracle or become native cache refusal.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_FailedOrIncompleteFulfillment_When_Observed_Then_OracleNeverRunsAndFailureSurvives(string provider)
    {
        Assert.False(string.IsNullOrWhiteSpace(provider));
        var primary = new IOException(Canary);
        var called = false;
        var budget = new HistoryObservationBudget();
        var observed = await Assert.ThrowsAsync<IOException>(() => budget.ObserveAsync(Task.FromException(primary),
            _ => { called = true; return Task.CompletedTask; }, TestContext.Current.CancellationToken));
        Assert.Same(primary, observed);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancelled.Cancel();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => budget.ObserveAsync(pending.Task,
            _ => { called = true; return Task.CompletedTask; }, cancelled.Token));
        Assert.False(called);
        Assert.False(pending.Task.IsCompleted);
    }

    /// <summary>An inspector that consumes the remaining budget before returning a pending task fails before waiting.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_InspectorConsumesRemainingBudget_When_PendingTaskIsReturned_Then_ObservationFailsBeforeWaiting(string provider)
    {
        Assert.False(string.IsNullOrWhiteSpace(provider));
        var clock = new ProbeClock();
        var budget = new HistoryObservationBudget(clock);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await Assert.ThrowsAsync<TimeoutException>(() => budget.ObserveAsync(Task.CompletedTask, _ =>
            {
                clock.Advance(ExpiredCost);
                return pending.Task;
            }, TestContext.Current.CancellationToken)).WaitAsync(TestDeadline, TestContext.Current.CancellationToken);
        }
        finally { pending.TrySetResult(); }
    }

    /// <summary>Unknown explicit modes and missing overlap evidence fail instead of silently falling back to ordinary execution.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public void Given_InvalidExperimentOrMissingWindow_When_Validated_Then_NoCompletedExperimentIsClaimed(string provider)
    {
        Assert.False(string.IsNullOrWhiteSpace(provider));
        Assert.Equal("ordinary", HistoryDiagnosticExperiment.SelectMode(null));
        Assert.Equal("isolated", HistoryDiagnosticExperiment.SelectMode(HistoryDiagnosticExperiment.Isolated));
        Assert.Equal("overlap", HistoryDiagnosticExperiment.SelectMode(HistoryDiagnosticExperiment.Overlap));
        Assert.Throws<InvalidOperationException>(() => HistoryDiagnosticExperiment.SelectMode(InvalidMode));
        Assert.Throws<InvalidOperationException>(() => HistoryDiagnosticExperiment.SelectMode(string.Empty));
        var window = new HistoryDiagnosticWindow();
        Assert.Throws<Xunit.Sdk.TrueException>(() => window.Require(true));
        Assert.Throws<Xunit.Sdk.TrueException>(() => window.Require(false));
    }

    /// <summary>A bounded join reports incomplete ownership without claiming pending or faulted cleanup has succeeded.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_PendingOrFaultedCleanup_When_BoundedJoinEnds_Then_IncompleteOwnershipIsReported(string provider)
    {
        Assert.False(string.IsNullOrWhiteSpace(provider));
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            Assert.False(await HistoryCleanup.JoinAsync(pending.Task, TimeSpan.Zero));
            Assert.False(pending.Task.IsCompleted);
        }
        finally { pending.TrySetResult(); }
        Assert.True(await HistoryCleanup.JoinAsync(pending.Task, TestDeadline));
        var delayedFault = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.False(await HistoryCleanup.JoinAsync(delayedFault.Task, TimeSpan.Zero));
        delayedFault.SetException(new IOException(Canary));
        Assert.False(await HistoryCleanup.JoinAsync(delayedFault.Task, TestDeadline));
        Assert.False(await HistoryCleanup.JoinAsync(Task.FromException(new IOException(Canary)), TestDeadline));
    }

    private sealed class ProbeClock : TimeProvider
    {
        private long ticks;
        /// <summary>Uses TimeSpan ticks so synthetic advances are exact and independent of wall-clock scheduling.</summary>
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        /// <summary>Returns the controlled monotonic timestamp.</summary>
        public override long GetTimestamp() => ticks;
        /// <summary>Advances the deterministic clock without sleeping.</summary>
        internal void Advance(TimeSpan duration) => ticks += duration.Ticks;
    }
}
