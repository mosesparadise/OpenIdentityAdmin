using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Checks background metadata rejection, independent request bindings and failed child acquisition cleanup.</summary>
public sealed class NativeServiceIsolationTests
{
    private const int ConcurrentRequests = 2;

    /// <summary>Requires explicit ownership even for cold metadata and key refresh, without claiming native profile interoperability.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_MetadataService_When_OwnershipIsExplicit_Then_OnlyOwnedRefreshCanCreateNativeScopes(bool keys)
    {
        await using var probe = new NativeServiceProbe();
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.ReadMetadataAsync(keys, TestContext.Current.CancellationToken));
        Assert.Empty(probe.Storage.Databases);
        var request = new ProtocolStorageRequest(probe.Storage.Clock, TestContext.Current.CancellationToken);
        await request.RunAsync(probe.Storage.Factory, async (_, token) =>
        {
            await probe.ReadMetadataAsync(keys, token);
            return true;
        });
        Assert.Equal(4, probe.Owners.Count);
        Assert.Equal(2, probe.Storage.Databases.Count);
        Assert.All(probe.Storage.Databases, database => Assert.Equal(1, database.Disposals));
    }

    /// <summary>Runs two requests against the same native singleton while preserving separate parent and child capabilities.</summary>
    [Fact]
    public async Task Given_ConcurrentRequests_When_NativeSingletonCreatesScopes_Then_ChildrenRemainIsolated()
    {
        await using var probe = new NativeServiceProbe();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        probe.Event = async () =>
        {
            if (Interlocked.Increment(ref entered) == ConcurrentRequests)
            {
                release.SetResult();
            }
            await release.Task.WaitAsync(TestContext.Current.CancellationToken);
        };
        var first = new ProtocolStorageRequest(probe.Storage.Clock, TestContext.Current.CancellationToken);
        var second = new ProtocolStorageRequest(probe.Storage.Clock, TestContext.Current.CancellationToken);
        await Task.WhenAll(first.RunAsync(probe.Storage.Factory, async (_, token) => { await probe.SendAsync(false, token); return true; }),
            second.RunAsync(probe.Storage.Factory, async (_, token) => { await probe.SendAsync(true, token); return true; }));
        Assert.Equal(2, probe.Owners.Distinct().Count());
        Assert.Equal(4, probe.Storage.Databases.Count);
        Assert.All(probe.Storage.Databases, database => Assert.Equal(1, database.Disposals));
    }

    /// <summary>Retains a child allocated before binding resolution fails and waits for its actual context disposal.</summary>
    [Fact]
    public async Task Given_MissingActivationBinding_When_ChildConstructionFails_Then_TrackedScopeStillDrains()
    {
        await using var probe = new NativeServiceProbe(services => services.RemoveAll<NativeClientActivation>());
        var request = new ProtocolStorageRequest(probe.Storage.Clock, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => request.RunAsync(probe.Storage.Factory, async (_, token) =>
        {
            await probe.SendAsync(false, token);
            return true;
        }));
        await request.DrainAsync().WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, probe.Storage.Databases.Count);
        Assert.All(probe.Storage.Databases, database => Assert.Equal(1, database.Disposals));
        Assert.Empty(probe.Owners);
    }
}
