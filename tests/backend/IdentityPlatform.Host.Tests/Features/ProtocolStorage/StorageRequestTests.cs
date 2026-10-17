using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Proves fresh scoped contexts cannot renew time or erase a request failure.</summary>
public sealed class StorageRequestTests
{
    private const int SecondScope = 2;
    private const int RequestAgeSeconds = 9;
    private const int DeadlineSeconds = 10;
    private const int WallClockDelayMilliseconds = 25;
    /// <summary>Checks original expiry before resolution, including an already-completed second operation.</summary>
    [Theory]
    [InlineData(9999, true)]
    [InlineData(10000, false)]
    [InlineData(10001, false)]
    public async Task Given_FreshScope_When_OriginalDeadlineExpires_Then_OriginalBudgetWins(int milliseconds, bool accepted)
    {
        await using var probe = new StorageRequestProbe(clock: new ControlledProbeClock());
        var request = new ProtocolStorageRequest(probe.Clock, TestContext.Current.CancellationToken);
        Assert.Equal(1, await request.RunAsync(probe.Factory, (scope, token) => new ValueTask<int>(probe.Databases.Count)));
        probe.Clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
        if (accepted)
        {
            Assert.Equal(SecondScope, await request.RunAsync(probe.Factory, async (scope, token) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(WallClockDelayMilliseconds), TestContext.Current.CancellationToken);
                Assert.False(token.IsCancellationRequested);
                return probe.Databases.Count;
            }));
            Assert.NotSame(probe.Databases[0], probe.Databases[1]);
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.RunAsync(probe.Factory, (scope, token) => new ValueTask<int>(0)));
            Assert.Single(probe.Databases);
        }
        Assert.All(probe.Databases, database => Assert.Equal(1, database.Disposals));
    }

    /// <summary>Starts a new transaction phase with only the original one-second remainder.</summary>
    [Fact]
    public async Task Given_PreviousScopeDisposed_When_NextPhaseStartsAtNineSeconds_Then_OnlyOneSecondRemains()
    {
        await using var probe = new StorageRequestProbe();
        var request = new ProtocolStorageRequest(probe.Clock, TestContext.Current.CancellationToken);
        await request.RunAsync(probe.Factory, (scope, token) => new ValueTask<int>(1));
        probe.Clock.Advance(TimeSpan.FromSeconds(RequestAgeSeconds));
        await request.RunAsync(probe.Factory, async (scope, token) =>
        {
            await using var phase = scope.Owner.CreateTransaction();
            Assert.Equal(TimeSpan.FromSeconds(1), scope.Owner.Capture().Lifetime.Remaining);
            return 1;
        });
        Assert.Equal(SecondScope, probe.Databases.Count);
        Assert.All(probe.Databases, database => Assert.Equal(1, database.Disposals));
    }

    /// <summary>Detects a swallowed owner fault and prevents a new scope from laundering it.</summary>
    [Fact]
    public async Task Given_SwallowedOwnerFailure_When_OperationReturns_Then_RequestRemainsTerminal()
    {
        await using var probe = new StorageRequestProbe();
        var request = new ProtocolStorageRequest(probe.Clock, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => request.RunAsync(probe.Factory, (scope, token) =>
        {
            Assert.Throws<InvalidOperationException>(() => scope.Owner.Capture(write: true));
            return new ValueTask<int>(1);
        }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => request.RunAsync(probe.Factory, (scope, token) => new ValueTask<int>(SecondScope)));
        Assert.Equal(1, Assert.Single(probe.Databases).Disposals);
    }

    /// <summary>Rejects overlap without queueing or acquiring a second scoped graph.</summary>
    [Fact]
    public async Task Given_PendingOperation_When_AnotherScopeIsRequested_Then_BothCannotSucceed()
    {
        await using var probe = new StorageRequestProbe();
        var request = new ProtocolStorageRequest(probe.Clock, TestContext.Current.CancellationToken);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = request.RunAsync(probe.Factory, (scope, token) => new ValueTask<int>(release.Task));
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => request.RunAsync(probe.Factory, (scope, token) => new ValueTask<int>(SecondScope)));
            Assert.Equal(0, Assert.Single(probe.Databases).Disposals);
        }
        finally { release.TrySetResult(1); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => first);
        Assert.Equal(1, Assert.Single(probe.Databases).Disposals);
    }

    /// <summary>Checks the deadline after a completed callback even when it performs no storage work.</summary>
    [Fact]
    public async Task Given_CompletedOperation_When_TimeExpiresInsideCallback_Then_ResultIsNotAccepted()
    {
        await using var probe = new StorageRequestProbe();
        var request = new ProtocolStorageRequest(probe.Clock, TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.RunAsync(probe.Factory, (scope, token) =>
        {
            probe.Clock.Advance(TimeSpan.FromSeconds(DeadlineSeconds));
            return new ValueTask<int>(1);
        }));
        Assert.Equal(1, Assert.Single(probe.Databases).Disposals);
    }
}
