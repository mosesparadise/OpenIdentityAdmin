namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Proves cleanup closes admission synchronously while cancellation callbacks run independently.</summary>
public sealed class StorageAdmissionTests
{
    /// <summary>Neither transaction nor scope disposal permits new native work before its asynchronous cleanup continuation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_DisposalStarted_When_NewWorkEntersImmediately_Then_AdmissionIsAlreadyClosed(bool transactionPhase)
    {
        await using var probe = new StorageProbe();
        await using var scope = await probe.OpenAsync(TestContext.Current.CancellationToken);
        await using var phase = transactionPhase ? scope.Owner.CreateTransaction() : null;
        var queued = new QueuedContext();
        var original = SynchronizationContext.Current;
        ValueTask pending;
        try
        {
            SynchronizationContext.SetSynchronizationContext(queued);
            pending = transactionPhase ? phase!.DisposeAsync() : scope.DisposeAsync();
            Assert.Throws<InvalidOperationException>(() => scope.Owner.Capture());
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(original);
            queued.Release();
        }
        await pending;
    }

    /// <summary>A reentrant cancellation callback joins the published cleanup task rather than launching another disposal.</summary>
    [Fact]
    public async Task Given_ReentrantCancellationCallback_When_DisposalRuns_Then_ChildIsDisposedExactlyOnce()
    {
        await using var probe = new StorageProbe();
        var scope = await probe.OpenAsync(TestContext.Current.CancellationToken);
        Task? reentrant = null;
        using var callback = scope.Owner.Capture().Lifetime.Token.Register(() => reentrant = scope.DisposeAsync().AsTask());
        await scope.DisposeAsync();
        Assert.NotNull(reentrant);
        await reentrant;
        Assert.Equal(1, probe.Database.Disposals);
    }

    /// <summary>A previously cancelled request cannot resolve or acquire the child context.</summary>
    [Fact]
    public async Task Given_PreCancelledRequest_When_OpeningOwner_Then_ContextIsNeverResolved()
    {
        await using var probe = new StorageProbe();
        using var request = new CancellationTokenSource();
        await request.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.OpenAsync(request.Token));
        Assert.Equal(0, probe.Database.Disposals);
        Assert.Equal(0, probe.Resolutions);
    }
    /// <summary>Holds asynchronous cleanup until the test has checked synchronous admission closure.</summary>
    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly object sync = new();
        private readonly List<Action> queued = [];
        private bool released;

        /// <summary>Queues continuations before release and dispatches them normally afterward.</summary>
        public override void Post(SendOrPostCallback callback, object? state)
        {
            lock (sync)
            {
                if (!released)
                {
                    queued.Add(() => callback(state));
                    return;
                }
            }
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }

        /// <summary>Releases every pending continuation without retaining this test synchronization context.</summary>
        internal void Release()
        {
            lock (sync)
            {
                released = true;
                foreach (var action in queued)
                {
                    ThreadPool.QueueUserWorkItem(_ => action());
                }
                queued.Clear();
            }
        }
    }
}
