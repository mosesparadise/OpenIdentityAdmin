using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Exercises the fresh child bridge within actual native server and client dispatch lifetimes.</summary>
public sealed class StorageChildTests
{
    private const int ParentAndChild = 2;
    private const int OriginalDeadlineSeconds = 10;

    /// <summary>Resumes the same native event only after its distinct child context has actually been disposed.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_NativeDispatch_When_FreshChildCompletes_Then_ParentResumesAfterDisposal(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        probe.Request = async context =>
        {
            var parent = ProtocolStorageAmbient.Current;
            var value = await ProtocolStorageChild.RunAsync(probe.Storage.Factory, (child, token) =>
            {
                Assert.Same(child, ProtocolStorageAmbient.Current!.Scope);
                Assert.Same(parent!.Request, ProtocolStorageAmbient.Current.Request);
                Assert.NotSame(parent.Scope.Owner.Database, child.Owner.Database);
                Assert.False(parent.Scope.Owner.Children.IsIdle);
                Assert.False(token.IsCancellationRequested);
                return new ValueTask<int>(ParentAndChild);
            });
            Assert.Equal(ParentAndChild, value);
            Assert.Same(parent, ProtocolStorageAmbient.Current);
            Assert.True(parent!.Scope.Owner.Children.IsIdle);
            Assert.Equal(1, probe.Storage.Databases[1].Disposals);
            parent.Scope.Owner.Capture();
            await probe.DispatchNestedAsync();
            context.Handle();
        };
        Assert.True(await probe.RunAsync(new(probe.Storage.Clock, TestContext.Current.CancellationToken)));
        Assert.All(probe.Storage.Databases, database => Assert.Equal(1, database.Disposals));
    }

    /// <summary>A caught child failure cannot be converted into a successful parent protocol response.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_FailedChild_When_NativeHandlerCatchesFailure_Then_OriginalRequestStaysTerminal(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        var expected = new ArithmeticException();
        probe.Request = async context =>
        {
            Assert.Same(expected, await Assert.ThrowsAsync<ArithmeticException>(() => ProtocolStorageChild.RunAsync<int>(
                probe.Storage.Factory, (_, _) => throw expected).AsTask()));
            Assert.True(probe.Active.Owner.Children.IsIdle);
            context.Handle();
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(new(probe.Storage.Clock, TestContext.Current.CancellationToken)));
        Assert.Equal(ParentAndChild, probe.Storage.Databases.Count);
        Assert.All(probe.Storage.Databases, database => Assert.Equal(1, database.Disposals));
    }

    /// <summary>The bridge borrows the original clock even when native work completes synchronously after expiry.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_FreshChild_When_OriginalDeadlineExpires_Then_NativeResultCannotEscape(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        probe.Request = async context =>
        {
            await ProtocolStorageChild.RunAsync(probe.Storage.Factory, (_, _) =>
            {
                probe.Storage.Clock.Advance(TimeSpan.FromSeconds(OriginalDeadlineSeconds));
                return new ValueTask<int>(1);
            });
            context.Handle();
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.RunAsync(new(probe.Storage.Clock, TestContext.Current.CancellationToken)));
        Assert.All(probe.Storage.Databases, database => Assert.Equal(1, database.Disposals));
    }

    /// <summary>Prevents native work in the parent from overlapping a child transaction or deferred child callback.</summary>
    [Fact]
    public async Task Given_ActiveChild_When_ParentAccessesStorage_Then_ParentAndChildCannotSucceed()
    {
        await using var probe = new StorageRequestProbe();
        var request = new ProtocolStorageRequest(probe.Clock, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => request.RunAsync(probe.Factory, async (parent, token) =>
            await ProtocolStorageChild.RunAsync(probe.Factory, (child, cancellation) =>
            {
                Assert.Throws<InvalidOperationException>(() => parent.Owner.Capture());
                return new ValueTask<int>(1);
            })));
        Assert.All(probe.Databases, database => Assert.Equal(1, database.Disposals));
    }

    /// <summary>Rejects creating an apparently independent child inside an uncommitted parent write phase.</summary>
    [Fact]
    public async Task Given_ParentWritePhase_When_ChildRequested_Then_NoChildIsAllocated()
    {
        await using var probe = new StorageRequestProbe();
        var request = new ProtocolStorageRequest(probe.Clock, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => request.RunAsync(probe.Factory, async (parent, token) =>
        {
            await using var phase = parent.Owner.CreateTransaction();
            return await ProtocolStorageChild.RunAsync(probe.Factory, (_, _) => new ValueTask<int>(1));
        }));
        Assert.Equal(1, Assert.Single(probe.Databases).Disposals);
    }

    /// <summary>Requires original request ownership before allocating any native scoped service.</summary>
    [Fact]
    public async Task Given_NoOriginalRequest_When_ChildRequested_Then_NoScopeIsAllocated()
    {
        await using var probe = new StorageRequestProbe();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ProtocolStorageChild.RunAsync(probe.Factory, (_, _) => new ValueTask<int>(1)).AsTask());
        Assert.Empty(probe.Databases);
    }
}
