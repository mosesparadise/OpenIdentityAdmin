using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Exercises partial DI acquisition before a child owner exists, including actual asynchronous disposal.</summary>
public sealed class StorageChildAcquisitionTests
{
    /// <summary>A failed owner resolution cannot release its parent lease while partially acquired native services are disposing.</summary>
    [Fact]
    public async Task Given_PartialChildAcquisition_When_OwnerResolutionFails_Then_ParentWaitsForActualCleanup()
    {
        await using var probe = new StorageRequestProbe();
        var cleanup = new BlockedCleanup();
        var expected = new ArithmeticException();
        var services = new ServiceCollection().AddScoped(_ => cleanup).AddScoped<ProtocolStorageOwner>(provider =>
        {
            provider.GetRequiredService<BlockedCleanup>();
            throw expected;
        });
        await using var failing = services.BuildServiceProvider();
        ProtocolStorageScope? parent = null;
        var request = new ProtocolStorageRequest(probe.Clock, TestContext.Current.CancellationToken);
        var pending = request.RunAsync(probe.Factory, async (scope, token) =>
        {
            parent = scope;
            return await ProtocolStorageChild.RunAsync(failing.GetRequiredService<IServiceScopeFactory>(), (_, _) => new ValueTask<int>(1));
        });
        try
        {
            await cleanup.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(parent);
            Assert.False(parent.Owner.Children.IsIdle);
            Assert.False(pending.IsCompleted);
            Assert.Equal(0, Assert.Single(probe.Databases).Disposals);
        }
        finally { cleanup.Release.TrySetResult(); }
        Assert.Same(expected, await Assert.ThrowsAsync<ArithmeticException>(() => pending));
        Assert.True(parent.Owner.Children.IsIdle);
        Assert.Equal(1, Assert.Single(probe.Databases).Disposals);
    }

    /// <summary>The installed native singleton's synchronous scope acquisition retains partial disposal even when that disposal fails.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_PartialNativeAcquisition_When_OwnerResolutionFails_Then_ParentRetainsCleanup(bool cleanupFails)
    {
        await using var parent = new StorageRequestProbe();
        var cleanup = new BlockedCleanup { Fail = cleanupFails };
        await using var native = new NativeServiceProbe(services =>
        {
            services.RemoveAll<ProtocolStorageOwner>();
            services.AddScoped(_ => cleanup).AddScoped<ProtocolStorageOwner>(provider =>
            {
                provider.GetRequiredService<BlockedCleanup>();
                throw new ArithmeticException();
            });
        });
        ProtocolStorageScope? captured = null;
        var request = new ProtocolStorageRequest(parent.Clock, TestContext.Current.CancellationToken);
        var pending = request.RunAsync(parent.Factory, async (scope, token) =>
        {
            captured = scope;
            await native.SendAsync(false, token);
            return true;
        });
        try
        {
            await cleanup.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(captured);
            Assert.False(captured.Owner.Children.IsIdle);
            Assert.False(pending.IsCompleted);
            Assert.Equal(0, Assert.Single(parent.Databases).Disposals);
        }
        finally { cleanup.Release.TrySetResult(); }
        Assert.NotNull(await Record.ExceptionAsync(() => pending));
        Assert.True(captured.Owner.Children.IsIdle);
        Assert.Equal(1, Assert.Single(parent.Databases).Disposals);
        Assert.Empty(native.Owners);
    }

    /// <summary>Represents a partially acquired native resource whose cleanup cannot finish until the test releases it.</summary>
    private sealed class BlockedCleanup : IAsyncDisposable
    {
        internal bool Fail
        {
            get; init;
        }
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Retains actual cleanup across the failed child constructor.</summary>
        public async ValueTask DisposeAsync()
        {
            Started.TrySetResult();
            await Release.Task;
            if (Fail)
            {
                throw new ArithmeticException();
            }
        }
    }
}
