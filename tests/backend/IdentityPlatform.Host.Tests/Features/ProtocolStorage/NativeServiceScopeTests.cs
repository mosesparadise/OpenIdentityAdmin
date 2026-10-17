using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Proves native service-owned scopes inherit live request authority and cannot bypass parent storage exclusion.</summary>
public sealed class NativeServiceScopeTests
{
    private const int DeadlineSeconds = 10;

    /// <summary>Runs all four actual native PAR/token stages in one fresh child and rejoins the parent before it can continue.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_OwnedRequest_When_NativeServiceSends_Then_ChildIsFreshAndActuallyDisposed(bool pushed)
    {
        await using var probe = new NativeServiceProbe();
        var request = new ProtocolStorageRequest(probe.Storage.Clock, TestContext.Current.CancellationToken);
        await request.RunAsync(probe.Storage.Factory, async (scope, token) =>
        {
            await probe.SendAsync(pushed, token);
            Assert.Equal(4, probe.Owners.Count);
            Assert.All(probe.Owners, owner => Assert.Same(probe.Owners[0], owner));
            Assert.NotSame(scope.Owner, probe.Owners[0]);
            Assert.Equal(0, probe.Storage.Databases[0].Disposals);
            Assert.Equal(1, probe.Storage.Databases[1].Disposals);
            scope.Owner.RequireHealthy();
            Assert.Same(scope, ProtocolStorageAmbient.Current!.Scope);
            return true;
        });
        await request.DrainAsync();
        Assert.Null(ProtocolStorageAmbient.Current);
        Assert.All(probe.Storage.Databases, database => Assert.Equal(1, database.Disposals));
    }

    /// <summary>Proves the public native API uses the same scope bridge and leaves ordinary application scopes untouched.</summary>
    [Fact]
    public async Task Given_PublicNativeChallenge_When_InvokedUnderOwnership_Then_ScopeFactoryRemainsApplicationOwned()
    {
        await using var probe = new NativeServiceProbe();
        var original = probe.Storage.Factory;
        var request = new ProtocolStorageRequest(probe.Storage.Clock, TestContext.Current.CancellationToken);
        await request.RunAsync(original, async (_, token) =>
        {
            var result = await probe.Service.ChallengeInteractivelyAsync(new()
            {
                CancellationToken = token
            });
            Assert.Equal("synthetic-nonce", result.Nonce);
            return true;
        });
        Assert.Same(original, probe.Storage.Root.GetRequiredService<IServiceScopeFactory>());
        Assert.Single(probe.Owners);
        Assert.Equal(2, probe.Storage.Databases.Count);
    }

    /// <summary>Rejects unbound calls before a context or transport handler can be created.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_NoRequest_When_NativeServiceSends_Then_NoChildIsCreated(bool pushed)
    {
        await using var probe = new NativeServiceProbe();
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.SendAsync(pushed, TestContext.Current.CancellationToken).AsTask());
        Assert.Empty(probe.Storage.Databases);
        Assert.Empty(probe.Owners);
    }

    /// <summary>Rejects transport creation while the parent owns an explicit transaction, even before its first command.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ParentTransaction_When_NativeServiceSends_Then_RequestIsTerminal(bool pushed)
    {
        await using var probe = new NativeServiceProbe();
        var request = new ProtocolStorageRequest(probe.Storage.Clock, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => request.RunAsync(probe.Storage.Factory, async (scope, token) =>
        {
            await using var transaction = scope.Owner.CreateTransaction();
            await Assert.ThrowsAsync<InvalidOperationException>(() => probe.SendAsync(pushed, token).AsTask());
            return true;
        }));
        Assert.Single(probe.Storage.Databases);
        Assert.Empty(probe.Owners);
    }

    /// <summary>Rejects an already-completed native service result at the original deadline without accepting a renewed child budget.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_NativeTransport_When_OriginalBudgetExpires_Then_CompletedResultIsRejected(bool pushed)
    {
        await using var probe = new NativeServiceProbe();
        probe.Event = () => { probe.Storage.Clock.Advance(TimeSpan.FromSeconds(DeadlineSeconds)); return ValueTask.CompletedTask; };
        var request = new ProtocolStorageRequest(probe.Storage.Clock, TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<Exception>(() => request.RunAsync(probe.Storage.Factory, async (_, token) =>
        {
            await probe.SendAsync(pushed, token);
            return true;
        }));
        Assert.Single(probe.Owners);
        Assert.All(probe.Storage.Databases, database => Assert.Equal(1, database.Disposals));
    }
}
