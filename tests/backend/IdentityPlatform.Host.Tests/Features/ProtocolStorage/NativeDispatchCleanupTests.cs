using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Verifies bounded caller cleanup never detaches actual native work or revives the original request.</summary>
public sealed class NativeDispatchCleanupTests
{
    private static readonly TimeSpan CompletionBound = TimeSpan.FromSeconds(8);

    /// <summary>A quarantined descendant keeps its context alive and completion never permits another child scope.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_OrphanedDispatch_When_CleanupTimesOut_Then_ActualCompletionCannotReviveRequest(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? nested = null;
        probe.Nested = async context => { await release.Task; context.Handle(); };
        probe.Request = context => { nested = probe.DispatchNestedAsync().AsTask(); context.Handle(); return ValueTask.CompletedTask; };
        var request = new ProtocolStorageRequest(probe.Storage.Clock, TestContext.Current.CancellationToken);
        var pending = probe.RunAsync(request);
        try
        {
            await Assert.ThrowsAsync<AggregateException>(() => pending.WaitAsync(CompletionBound, TestContext.Current.CancellationToken));
            Assert.False(nested!.IsCompleted);
            Assert.False(probe.Active.Completion.IsCompleted);
            Assert.Equal(0, Assert.Single(probe.Storage.Databases).Disposals);
            await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(request));
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => nested!.WaitAsync(CompletionBound, TestContext.Current.CancellationToken));
        await probe.Active.Completion.WaitAsync(CompletionBound, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(request));
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }
}
