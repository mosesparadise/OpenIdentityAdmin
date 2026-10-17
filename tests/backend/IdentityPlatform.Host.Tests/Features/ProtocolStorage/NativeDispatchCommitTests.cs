using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Checks commit admission against the real installed dispatch stack before any database commit.</summary>
public sealed class NativeDispatchCommitTests
{
    /// <summary>The executing leaf can reach the commit guard while a parent with unfinished nested work cannot.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_PendingNestedDispatch_When_ParentChecksCommit_Then_RequestFailsBeforeCommit(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.Nested = async context =>
        {
            probe.Active.Owner.RequireNoReaders();
            await release.Task;
            context.Handle();
        };
        probe.Request = async context =>
        {
            var pending = probe.DispatchNestedAsync().AsTask();
            try
            {
                Assert.Throws<InvalidOperationException>(probe.Active.Owner.RequireNoReaders);
            }
            finally { release.TrySetResult(); }
            await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
            context.Handle();
        };
        var request = new ProtocolStorageRequest(probe.Storage.Clock, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(request));
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(request));
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }

    /// <summary>A pending native operation owns the context until completion and cannot admit another dispatcher.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_OccupiedNativeExecution_When_NestedDispatchStarts_Then_ChildIsRejectedBeforeHandler(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        var calls = 0;
        probe.Nested = context => { calls++; context.Handle(); return ValueTask.CompletedTask; };
        probe.Request = async context =>
        {
            using var execution = probe.Active.Owner.Enter(probe.Active.Owner.Capture(), context.ReadToken());
            await Assert.ThrowsAsync<InvalidOperationException>(() => probe.DispatchNestedAsync().AsTask());
            context.Handle();
        };
        var request = new ProtocolStorageRequest(probe.Storage.Clock, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(request));
        Assert.Equal(0, calls);
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }

    /// <summary>An independent incoming native token remains linked to the original request and is restored on failure.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_IndependentNativeToken_When_ItIsCancelled_Then_DispatchFailsAndRestoresOriginalBinding(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        using var incoming = new CancellationTokenSource();
        probe.IncomingToken = incoming.Token;
        probe.Request = context =>
        {
            Assert.NotEqual(incoming.Token, context.ReadToken());
            incoming.Cancel();
            Assert.True(context.ReadToken().IsCancellationRequested);
            context.Handle();
            return ValueTask.CompletedTask;
        };
        var request = new ProtocolStorageRequest(probe.Storage.Clock, TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.RunAsync(request));
        Assert.Equal(incoming.Token, probe.CurrentToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(request));
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }
}
