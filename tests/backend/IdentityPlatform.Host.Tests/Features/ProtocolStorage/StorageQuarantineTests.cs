using System.Runtime.ExceptionServices;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Proves bounded caller cleanup retains the entire scope until noncooperative native work actually stops.</summary>
public sealed class StorageQuarantineTests
{
    private static readonly TimeSpan CompletionBound = TimeSpan.FromSeconds(5);
    /// <summary>A timed-out wait cannot dispose a context still used by a real pending native operation.</summary>
    [Fact]
    public async Task Given_NoncooperativeNativeCall_When_CleanupTimesOut_Then_ContextRemainsOwnedUntilCompletion()
    {
        await using var probe = new StorageProbe();
        var scope = await probe.OpenAsync(TestContext.Current.CancellationToken);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = scope.Owner.RunAsync(_ => new ValueTask<int>(release.Task), CancellationToken.None).AsTask();
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.DisposeAsync().AsTask());
            Assert.False(pending.IsCompleted);
            Assert.False(scope.Completion.IsCompleted);
            Assert.Equal(0, probe.Database.Disposals);
            Assert.Throws<InvalidOperationException>(() => scope.Owner.Capture());
        }
        finally { release.TrySetResult(1); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        await scope.Completion.WaitAsync(CompletionBound, TestContext.Current.CancellationToken);
        await scope.DisposeAsync();
        Assert.Equal(1, probe.Database.Disposals);
    }

    /// <summary>A blocking cancellation callback cannot prevent the cleanup wait from reaching its own timeout.</summary>
    [Fact]
    public async Task Given_BlockingCancellationCallback_When_ScopeCloses_Then_CallerWaitIsBoundedAndCleanupRemainsSingle()
    {
        await using var probe = new StorageProbe();
        var scope = await probe.OpenAsync(TestContext.Current.CancellationToken);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var callback = scope.Owner.Capture().Lifetime.Token.Register(() =>
        {
            entered.SetResult();
            release.Wait(TestContext.Current.CancellationToken);
        });
        List<Exception> failures = [];
        var disposal = Task.Run(() => scope.DisposeAsync().AsTask(), CancellationToken.None);
        try
        {
            await entered.Task.WaitAsync(CompletionBound, TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidOperationException>(() => disposal);
            Assert.Equal(0, probe.Database.Disposals);
            Assert.False(scope.Completion.IsCompleted);
        }
        catch (Exception error) { failures.Add(error); }
        finally { release.Set(); }
        await FinishCallbackAsync(scope, disposal, probe, failures);
    }

    /// <summary>Joins invocation and retained cleanup even after callback-admission failure, preserving the original failure alongside teardown errors.</summary>
    private static async Task FinishCallbackAsync(ProtocolStorageScope scope, Task disposal, StorageProbe probe, List<Exception> failures)
    {
        try
        {
            await disposal.WaitAsync(CompletionBound, CancellationToken.None);
        }
        catch (InvalidOperationException) { }
        catch (Exception error) { failures.Add(error); }
        try
        {
            await scope.Completion.WaitAsync(CompletionBound, CancellationToken.None);
            await scope.DisposeAsync();
            Assert.Equal(1, probe.Database.Disposals);
        }
        catch (Exception error) { failures.Add(error); }
        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
        if (failures.Count > 1)
        {
            throw new AggregateException(failures);
        }
    }
}
