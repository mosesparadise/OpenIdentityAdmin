namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Ensures native dispatcher tasks cannot outlive their services or race shared cancellation bindings.</summary>
public sealed class NativeDispatchConcurrencyTests
{
    private static readonly TimeSpan CompletionBound = TimeSpan.FromSeconds(5);

    /// <summary>An unawaited nested dispatcher is retained by final cleanup until its actual handler stops.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_UnawaitedNestedDispatch_When_ParentReturns_Then_CleanupRetainsChildServices(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? nested = null;
        probe.Nested = async context => { await release.Task; context.Handle(); };
        probe.Request = context => { nested = probe.DispatchNestedAsync().AsTask(); context.Handle(); return ValueTask.CompletedTask; };
        var pending = probe.RunAsync(new(probe.Storage.Clock, TestContext.Current.CancellationToken));
        try
        {
            Assert.NotNull(nested);
            Assert.False(nested.IsCompleted);
            Assert.False(pending.IsCompleted);
            Assert.Equal(0, Assert.Single(probe.Storage.Databases).Disposals);
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => nested!.WaitAsync(CompletionBound, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => pending.WaitAsync(CompletionBound, TestContext.Current.CancellationToken));
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }

    /// <summary>A second sibling cannot overwrite the token of a pending nested native dispatch.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_PendingNestedDispatch_When_SiblingStarts_Then_RequestFailsBeforeSecondHandler(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        probe.Nested = async context => { calls++; await release.Task; context.Handle(); };
        probe.Request = async context =>
        {
            var pending = probe.DispatchNestedAsync().AsTask();
            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => probe.DispatchNestedAsync().AsTask());
            }
            finally { release.TrySetResult(); }
            await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
            context.Handle();
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(new(probe.Storage.Clock, TestContext.Current.CancellationToken)));
        Assert.Equal(1, calls);
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }

    /// <summary>Rejects disposal from inside its own dispatch lease rather than deadlocking cleanup on that lease.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ActiveNativeHandler_When_ItDisposesItsOwnScope_Then_ReentrancyIsRejected(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        probe.Request = async context =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => probe.Active.DisposeAsync().AsTask());
            Assert.True(probe.Active.Completion.IsCompleted);
            context.Handle();
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(new(probe.Storage.Clock, TestContext.Current.CancellationToken)));
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }

    /// <summary>Cancellation does not detach the real native handler or dispose services before it finishes.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_NoncooperativeNativeHandler_When_RequestIsCancelled_Then_ActualCompletionPrecedesDisposal(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        using var cancellation = new CancellationTokenSource();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.Request = async context => { await release.Task; context.Handle(); };
        var pending = probe.RunAsync(new(probe.Storage.Clock, cancellation.Token));
        try
        {
            cancellation.Cancel();
            Assert.False(pending.IsCompleted);
            Assert.Equal(0, Assert.Single(probe.Storage.Databases).Disposals);
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(CompletionBound, TestContext.Current.CancellationToken));
        Assert.Equal(probe.InitialToken, probe.CurrentToken);
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }
}
