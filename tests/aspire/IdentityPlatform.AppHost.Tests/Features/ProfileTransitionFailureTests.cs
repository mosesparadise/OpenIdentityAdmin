using System.Runtime.CompilerServices;
using Aspire.Hosting.ApplicationModel;

namespace IdentityPlatform.AppHost.Tests.Features;

[Trait("Category", "Harness")]
public sealed class ProfileTransitionFailureTests
{
    private const string Worker = "worker";
    private const string Canary = "synthetic-private-observer-failure";
    private const string ProjectKind = "project";
    private static readonly TimeSpan TestDeadline = TimeSpan.FromSeconds(5);

    /// <summary>Sanitization and enumerator disposal faults preserve the primary error and do not stop subsequent safe capture.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_MalformedEventOrDisposalFault_When_Observed_Then_OriginalFailureAndDiagnosticGapSurvive(bool disposalFault)
    {
        var lines = new List<string>();
        var primary = new InvalidOperationException(Canary);
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => ProfileTransitionObserver.ObserveAsync(
            token => FaultingStreamAsync(disposalFault, token), _ => Task.FromException(primary), lines.Add));
        Assert.Same(primary, observed);
        Assert.Contains("observer-failed=True", lines[0], StringComparison.Ordinal);
        const string expectedFinished = "state=Finished";
        Assert.Contains(lines, line => line.Contains(expectedFinished, StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains(Canary, StringComparison.Ordinal));
    }

    /// <summary>Bounds even a blocked cancellation callback and releases the controlled callback after the assertion.</summary>
    [Fact]
    public async Task Given_BlockedCancellationCallback_When_Stopping_Then_JoinIsBoundedAndIncompleteIsExplicit()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lines = new List<string>();
        try
        {
            await ProfileTransitionObserver.ObserveAsync(token => BlockedStreamAsync(release.Task, disposed, token),
                _ => Task.CompletedTask, lines.Add).WaitAsync(TestDeadline, TestContext.Current.CancellationToken);
            Assert.Contains("observer-failed=True joined=False", lines[0], StringComparison.Ordinal);
        }
        finally
        {
            release.TrySetResult();
            await disposed.Task.WaitAsync(TestDeadline, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>A noncooperative reader retains cleanup ownership after timeout and is explicitly released by this control.</summary>
    [Fact]
    public async Task Given_CancellationIgnoringReader_When_Stopping_Then_IncompleteJoinIsReportedAndCleanupFinishesLater()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lines = new List<string>();
        try
        {
            await ProfileTransitionObserver.ObserveAsync(_ => IgnoringStreamAsync(release.Task, disposed),
                _ => Task.CompletedTask, lines.Add).WaitAsync(TestDeadline, TestContext.Current.CancellationToken);
            Assert.Contains("observer-failed=True joined=False", lines[0], StringComparison.Ordinal);
            Assert.False(disposed.Task.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
            await disposed.Task.WaitAsync(TestDeadline, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Models an upstream reader ignoring cancellation, with a test-owned release and observable disposal.</summary>
    private static async IAsyncEnumerable<ResourceEvent> IgnoringStreamAsync(Task release, TaskCompletionSource disposed)
    {
        try
        {
            await release;
        }
        finally { disposed.TrySetResult(); }
        yield break;
    }

    /// <summary>Contains exceptions thrown by cancellation callbacks without replacing a primary test failure.</summary>
    [Fact]
    public async Task Given_ThrowingCancellationCallback_When_Stopping_Then_PrimaryErrorIsPreserved()
    {
        var primary = new InvalidOperationException(Canary);
        var lines = new List<string>();
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => ProfileTransitionObserver.ObserveAsync(
            ThrowingStreamAsync, _ => Task.FromException(primary), lines.Add).WaitAsync(TestDeadline, TestContext.Current.CancellationToken));
        Assert.Same(primary, observed);
        Assert.Contains("observer-failed=True", lines[0], StringComparison.Ordinal);
    }

    /// <summary>Yields an invalid metadata array followed by safe terminal state, optionally faulting at disposal.</summary>
    private static async IAsyncEnumerable<ResourceEvent> FaultingStreamAsync(bool disposalFault, [EnumeratorCancellation] CancellationToken token)
    {
        var worker = new ProjectResource(Worker);
        var snapshot = new CustomResourceSnapshot { ResourceType = ProjectKind, Properties = [], State = new(KnownResourceStates.Finished, null) };
        await using var disposal = new ProbeDisposal(disposalFault);
        if (!disposalFault)
        {
            yield return new ResourceEvent(worker, Worker, snapshot with
            {
                Properties = default
            });
        }
        yield return new ResourceEvent(worker, Worker, snapshot);
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
    }

    private sealed class ProbeDisposal(bool fail) : IAsyncDisposable
    {
        /// <summary>Models failure from an upstream enumerator's asynchronous disposal contract.</summary>
        public ValueTask DisposeAsync() => fail ? ValueTask.FromException(new IOException(Canary)) : ValueTask.CompletedTask;
    }

    /// <summary>Owns a deliberately blocked callback whose release and final disposal are observable to the test.</summary>
    private static async IAsyncEnumerable<ResourceEvent> BlockedStreamAsync(Task release, TaskCompletionSource disposed,
        [EnumeratorCancellation] CancellationToken token)
    {
        using var registration = token.Register(() => release.GetAwaiter().GetResult());
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }
        finally
        {
            await release;
            disposed.TrySetResult();
        }
        yield break;
    }

    /// <summary>Registers a hostile callback independently of enumerator cancellation/disposal behavior.</summary>
    private static async IAsyncEnumerable<ResourceEvent> ThrowingStreamAsync([EnumeratorCancellation] CancellationToken token)
    {
        using var registration = token.Register(() => throw new IOException(Canary));
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
        yield break;
    }
}
