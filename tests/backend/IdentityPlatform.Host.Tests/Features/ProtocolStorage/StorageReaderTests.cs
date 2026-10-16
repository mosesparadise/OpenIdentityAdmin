using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Separates deferred reader lifetime, actual execution, disposal and monotonic epoch ownership.</summary>
public sealed class StorageReaderTests
{
    private const int GetterValue = 42;
    private const int OverlappingValue = 2;
    private const int NativeRowValue = 7;
    private const string DisposalFailure = "synthetic disposal failure";
    /// <summary>Allows sequential getters between yields while retaining the reader until its actual disposal.</summary>
    [Fact]
    public async Task Given_PausedReader_When_SequentialGetterRuns_Then_ExecutionIsAllowedButReaderStillOwned()
    {
        await using var probe = new StorageProbe();
        await using var scope = await probe.OpenAsync(TestContext.Current.CancellationToken);
        var native = new Reader();
        await using (var reader = scope.Owner.Enumerate(_ => native, CancellationToken.None).GetAsyncEnumerator(TestContext.Current.CancellationToken))
        {
            Assert.True(await reader.MoveNextAsync());
            Assert.Equal(7, reader.Current);
            Assert.Equal(42, await scope.Owner.RunAsync(_ => new ValueTask<int>(GetterValue), CancellationToken.None));
            Assert.Equal(0, native.Disposals);
        }
        Assert.Equal(1, native.Disposals);
        await using var transaction = scope.Owner.CreateTransaction();
    }

    /// <summary>Does not let a source returned in a closed epoch adopt the next phase.</summary>
    [Fact]
    public async Task Given_CapturedSource_When_PhaseEnds_Then_EnumerationCannotStart()
    {
        await using var probe = new StorageProbe();
        await using var scope = await probe.OpenAsync(TestContext.Current.CancellationToken);
        var native = new Reader();
        var source = scope.Owner.Enumerate(_ => native, CancellationToken.None);
        await scope.Owner.CreateTransaction().DisposeAsync();
        Assert.Throws<InvalidOperationException>(() => source.GetAsyncEnumerator(TestContext.Current.CancellationToken));
        Assert.Equal(0, native.Moves);
    }

    /// <summary>Rejects overlapping native work immediately and retains occupancy until the first operation truly finishes.</summary>
    [Fact]
    public async Task Given_PendingNativeCall_When_AnotherCallStarts_Then_OverlapFaultsAndCleanupWaits()
    {
        await using var probe = new StorageProbe();
        var scope = await probe.OpenAsync(TestContext.Current.CancellationToken);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = scope.Owner.RunAsync(_ => new ValueTask<int>(release.Task), CancellationToken.None).AsTask();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Owner.RunAsync(_ => new ValueTask<int>(OverlappingValue), CancellationToken.None).AsTask());
        var cleanup = scope.DisposeAsync().AsTask();
        Assert.False(cleanup.IsCompleted);
        Assert.Equal(0, probe.Database.Disposals);
        release.SetResult(1);
        await Assert.ThrowsAnyAsync<Exception>(() => pending);
        await cleanup;
        Assert.Equal(1, probe.Database.Disposals);
    }

    /// <summary>Continues draining other readers and disposes the child context while reporting native disposal failure.</summary>
    [Fact]
    public async Task Given_FailedReaderDisposal_When_ScopeCloses_Then_AllCleanupRunsAndFailureIsReported()
    {
        await using var probe = new StorageProbe();
        var scope = await probe.OpenAsync(TestContext.Current.CancellationToken);
        var failed = new Reader { FailDisposal = true };
        var other = new Reader();
        var first = scope.Owner.Enumerate(_ => failed, CancellationToken.None).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var second = scope.Owner.Enumerate(_ => other, CancellationToken.None).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        Assert.True(await first.MoveNextAsync());
        Assert.True(await second.MoveNextAsync());
        await Assert.ThrowsAsync<AggregateException>(() => scope.DisposeAsync().AsTask());
        Assert.Equal(1, failed.Disposals);
        Assert.Equal(1, other.Disposals);
        Assert.Equal(1, probe.Database.Disposals);
    }

    /// <summary>A throwing reader cancellation callback is reported after native disposal and scope cleanup still completes.</summary>
    [Fact]
    public async Task Given_ThrowingReaderCancellation_When_Disposed_Then_FailureIsReportedAfterNativeCleanup()
    {
        await using var probe = new StorageProbe();
        var scope = await probe.OpenAsync(TestContext.Current.CancellationToken);
        var native = new Reader();
        CancellationTokenRegistration callback = default;
        var reader = scope.Owner.Enumerate(token =>
        {
            callback = token.Register(() => throw new InvalidOperationException(DisposalFailure));
            return native;
        }, CancellationToken.None).GetAsyncEnumerator(TestContext.Current.CancellationToken);
        Assert.True(await reader.MoveNextAsync());
        await Assert.ThrowsAsync<AggregateException>(() => reader.DisposeAsync().AsTask());
        await callback.DisposeAsync();
        Assert.Equal(1, native.Disposals);
        await scope.DisposeAsync();
        Assert.Equal(1, probe.Database.Disposals);
    }

    /// <summary>Provides observable real enumerator execution without a database or substituted owner.</summary>
    private sealed class Reader : IAsyncEnumerable<int>, IAsyncEnumerator<int>
    {
        internal int Moves
        {
            get; private set;
        }
        internal int Disposals
        {
            get; private set;
        }
        internal bool FailDisposal
        {
            get; init;
        }
        public int Current => NativeRowValue;
        /// <summary>Returns this controlled native enumerator.</summary>
        public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;
        /// <summary>Yields once so relationship calls can run while reader lifetime is outstanding.</summary>
        public ValueTask<bool> MoveNextAsync() => new(++Moves == 1);
        /// <summary>Records completion even when disposal itself reports failure.</summary>
        public ValueTask DisposeAsync()
        {
            Disposals++;
            return FailDisposal ? ValueTask.FromException(new InvalidOperationException(DisposalFailure)) : ValueTask.CompletedTask;
        }
    }
}
