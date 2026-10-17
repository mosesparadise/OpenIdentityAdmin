using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Checks original cancellation and cleanup outcomes independently of native store calls.</summary>
public sealed class StorageRequestCleanupTests
{
    private const int DeadlineSeconds = 10;

    /// <summary>Cancelling the original token between scopes prevents any subsequent native context acquisition.</summary>
    [Fact]
    public async Task Given_IdleRequest_When_OriginalCancellationArrives_Then_NextScopeIsNotResolved()
    {
        await using var probe = new StorageRequestProbe();
        using var cancellation = new CancellationTokenSource();
        var request = new ProtocolStorageRequest(probe.Clock, cancellation.Token);
        await request.RunAsync(probe.Factory, (_, _) => new ValueTask<int>(1));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.RunAsync(probe.Factory, (_, _) => new ValueTask<int>(1)));
        Assert.Equal(1, Assert.Single(probe.Databases).Disposals);
    }

    /// <summary>Expiry during child disposal still prevents acceptance after callback completion.</summary>
    [Fact]
    public async Task Given_CompletedCallback_When_CleanupCrossesOriginalDeadline_Then_ResultIsRejected()
    {
        Action cleanup = () => { };
        await using var probe = new StorageRequestProbe(services => services.AddScoped(_ => new Cleanup(() => cleanup())));
        cleanup = () => probe.Clock.Advance(TimeSpan.FromSeconds(DeadlineSeconds));
        var request = new ProtocolStorageRequest(probe.Clock, TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.RunAsync(probe.Factory, (scope, _) =>
        {
            scope.Services.GetRequiredService<Cleanup>();
            return new ValueTask<int>(1);
        }));
        Assert.Equal(1, Assert.Single(probe.Databases).Disposals);
    }

    /// <summary>A cleanup failure is terminal even after an otherwise successful callback.</summary>
    [Fact]
    public async Task Given_ThrowingChildDisposal_When_CleanupFails_Then_RequestCannotAcquireAnotherScope()
    {
        var expected = new ArithmeticException();
        await using var probe = new StorageRequestProbe(services => services.AddScoped(_ => new Cleanup(() => throw expected)));
        var request = new ProtocolStorageRequest(probe.Clock, TestContext.Current.CancellationToken);
        var failure = await Assert.ThrowsAsync<AggregateException>(() => request.RunAsync(probe.Factory, (scope, _) =>
        {
            scope.Services.GetRequiredService<Cleanup>();
            return new ValueTask<int>(1);
        }));
        Assert.Contains(expected, failure.Flatten().InnerExceptions);
        await Assert.ThrowsAsync<InvalidOperationException>(() => request.RunAsync(probe.Factory, (_, _) => new ValueTask<int>(1)));
    }

    /// <summary>Rejects clock substitution before native work can adopt a mismatched monotonic origin.</summary>
    [Fact]
    public async Task Given_DifferentClock_When_ScopeStarts_Then_OwnershipFailsAndAcquiredContextIsDisposed()
    {
        await using var probe = new StorageRequestProbe();
        var request = new ProtocolStorageRequest(TimeProvider.System, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => request.RunAsync(probe.Factory, (_, _) => new ValueTask<int>(1)));
        Assert.Equal(1, Assert.Single(probe.Databases).Disposals);
    }

    /// <summary>Supplies a real DI-owned cleanup callback with no database side effects.</summary>
    private sealed class Cleanup(Action action) : IDisposable
    {
        /// <summary>Runs when the actual child provider is disposed.</summary>
        public void Dispose() => action();
    }
}
