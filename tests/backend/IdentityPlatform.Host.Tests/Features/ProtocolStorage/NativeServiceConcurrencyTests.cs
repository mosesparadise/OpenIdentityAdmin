using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Proves ambient native child capabilities cannot be reused by siblings or detached stale continuations.</summary>
public sealed class NativeServiceConcurrencyTests
{
    /// <summary>Rejects a sibling without acquiring another context and retains the original actual child until it finishes.</summary>
    [Fact]
    public async Task Given_BlockedNativeChild_When_SiblingStarts_Then_RequestFailsWithoutEarlyDisposal()
    {
        await using var probe = new NativeServiceProbe();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.Event = () => new(release.Task);
        var request = new ProtocolStorageRequest(probe.Storage.Clock, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => request.RunAsync(probe.Storage.Factory, async (_, token) =>
        {
            var child = probe.SendAsync(false, token).AsTask();
            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => probe.SendAsync(true, token).AsTask());
                Assert.Equal(2, probe.Storage.Databases.Count);
                Assert.All(probe.Storage.Databases, database => Assert.Equal(0, database.Disposals));
            }
            finally { release.TrySetResult(); }
            await Assert.ThrowsAsync<InvalidOperationException>(() => child);
            return true;
        }));
        Assert.All(probe.Storage.Databases, database => Assert.Equal(1, database.Disposals));
    }

    /// <summary>Rejects a capability captured by a detached continuation after its parent scope has been disposed.</summary>
    [Fact]
    public async Task Given_CapturedAmbient_When_ParentHasReturned_Then_StaleNativeCallCreatesNoChild()
    {
        await using var probe = new NativeServiceProbe();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? detached = null;
        var request = new ProtocolStorageRequest(probe.Storage.Clock, TestContext.Current.CancellationToken);
        await request.RunAsync(probe.Storage.Factory, (_, token) =>
        {
            detached = Task.Run(async () => { await release.Task; await probe.SendAsync(false, TestContext.Current.CancellationToken); }, TestContext.Current.CancellationToken);
            return new ValueTask<bool>(true);
        });
        release.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => detached!);
        Assert.Single(probe.Storage.Databases);
        Assert.Empty(probe.Owners);
    }

    /// <summary>Rejects a service call initiated above a pending nested dispatch before native transport scope creation.</summary>
    [Fact]
    public async Task Given_PendingNestedDispatch_When_ParentStartsNativeService_Then_OnlyActiveLeafMayCreateChild()
    {
        await using var probe = new NativeServiceProbe();
        var request = new ProtocolStorageRequest(probe.Storage.Clock, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => request.RunAsync(probe.Storage.Factory, async (scope, token) =>
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var binding = token;
            var pending = ProtocolStorageDispatch.RunAsync(scope.Owner, () => binding, value => binding = value, () => new(release.Task)).AsTask();
            try
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => probe.SendAsync(false, token).AsTask());
            }
            finally { release.SetResult(); }
            await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
            return true;
        }));
        Assert.Single(probe.Storage.Databases);
        Assert.Empty(probe.Owners);
    }
}
