using System.Runtime.CompilerServices;
using Aspire.Hosting.ApplicationModel;

namespace IdentityPlatform.AppHost.Tests.Features;

[Trait("Category", "Harness")]
public sealed class ProfileTransitionObserverTests
{
    private const int FailureExitCode = 2;
    private const string Canary = "synthetic-private-observer-error";
    private static readonly TimeSpan TestDeadline = TimeSpan.FromSeconds(5);

    /// <summary>Subscribes before the action and stops/disposes a waiting reader even when the original action fails immediately.</summary>
    [Fact]
    public async Task Given_ImmediateActionFailure_When_Observed_Then_SubscriptionPrecedesActionAndOriginalErrorSurvives()
    {
        var stream = new WaitingStream();
        var primary = new InvalidOperationException(Canary);
        var lines = new List<string>();
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => ProfileTransitionObserver.ObserveAsync(stream.ReadAsync,
            _ => { Assert.True(stream.Subscribed); return Task.FromException(primary); }, lines.Add).WaitAsync(TestDeadline, TestContext.Current.CancellationToken));
        Assert.Same(primary, observed);
        Assert.True(stream.Disposed);
        Assert.Contains("publication-drain=not-confirmed", lines[0], StringComparison.Ordinal);
        Assert.DoesNotContain(lines, line => line.Contains(Canary, StringComparison.Ordinal));
    }

    /// <summary>Reader and output failures preserve the exact original cancellation and continue bounded output attempts.</summary>
    [Fact]
    public async Task Given_ObserverAndOutputFailures_When_ActionCancels_Then_OriginalCancellationSurvives()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var primary = new OperationCanceledException(source.Token);
        var writes = 0;
        var observed = await Assert.ThrowsAsync<OperationCanceledException>(() => ProfileTransitionObserver.ObserveAsync(
            _ => throw new IOException(Canary), _ => Task.FromException(primary), _ => { writes++; throw new IOException(Canary); }));
        Assert.Same(primary, observed);
        Assert.Equal(source.Token, observed.CancellationToken);
        Assert.Equal(13, writes);
    }

    /// <summary>Successful action plus observer failure emits an explicit diagnostic gap rather than implying complete observation.</summary>
    [Fact]
    public async Task Given_ReaderFailure_When_ActionSucceeds_Then_DiagnosticGapIsReported()
    {
        var lines = new List<string>();
        await ProfileTransitionObserver.ObserveAsync(_ => throw new IOException(Canary), _ => Task.CompletedTask, lines.Add);
        Assert.Contains("observer-failed=True", lines[0], StringComparison.Ordinal);
        Assert.DoesNotContain(lines, line => line.Contains(Canary, StringComparison.Ordinal));
    }

    /// <summary>Successful execution remains silent while still joining and disposing its active observer.</summary>
    [Fact]
    public async Task Given_Success_When_Observed_Then_NoOutputIsWrittenAndReaderIsDisposed()
    {
        var stream = new WaitingStream();
        var lines = new List<string>();
        await ProfileTransitionObserver.ObserveAsync(stream.ReadAsync, _ => Task.CompletedTask,
            lines.Add).WaitAsync(TestDeadline, TestContext.Current.CancellationToken);
        Assert.Empty(lines);
        Assert.True(stream.Subscribed);
        Assert.True(stream.Disposed);
    }

    private sealed class WaitingStream
    {
        internal bool Subscribed
        {
            get; private set;
        }
        internal bool Disposed
        {
            get; private set;
        }

        /// <summary>Models an idle subscription with observable ownership and cancellation-aware completion.</summary>
        internal async IAsyncEnumerable<ResourceEvent> ReadAsync([EnumeratorCancellation] CancellationToken token)
        {
            Subscribed = true;
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            finally { Disposed = true; }
            yield break;
        }
    }
}
