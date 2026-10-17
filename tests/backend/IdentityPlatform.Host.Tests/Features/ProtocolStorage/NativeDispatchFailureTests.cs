namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Closes native filter, unowned-scope and completed-result expiry bypasses.</summary>
public sealed class NativeDispatchFailureTests
{
    private const int DeadlineSeconds = 10;

    /// <summary>Native filters can fail before handlers and must still release the same owned graph.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ThrowingNativeFilter_When_DispatchBegins_Then_HandlerDoesNotRunAndCleanupCompletes(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        var expected = new ArithmeticException();
        var calls = 0;
        probe.Filter = () => throw expected;
        probe.Request = context => { calls++; context.Handle(); return ValueTask.CompletedTask; };
        Assert.Same(expected, await Assert.ThrowsAsync<ArithmeticException>(() => probe.RunAsync(new(probe.Storage.Clock, TestContext.Current.CancellationToken))));
        Assert.Equal(0, calls);
        Assert.Equal(probe.InitialToken, probe.CurrentToken);
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }

    /// <summary>An independently created native service scope cannot silently adopt a new request budget.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_UnownedNativeScope_When_DispatchIsAttempted_Then_NoHandlerIsAdmitted(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        var calls = 0;
        probe.Request = context => { calls++; context.Handle(); return ValueTask.CompletedTask; };
        await Assert.ThrowsAsync<InvalidOperationException>(probe.DispatchUnownedAsync);
        Assert.Equal(0, calls);
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }

    /// <summary>A completed native handled result cannot bypass the original strict deadline.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_CompletedNativeResult_When_OriginalTimeExpires_Then_OuterFlowRejectsIt(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        probe.Request = context =>
        {
            probe.Storage.Clock.Advance(TimeSpan.FromSeconds(DeadlineSeconds));
            context.Handle();
            return ValueTask.CompletedTask;
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.RunAsync(new(probe.Storage.Clock, TestContext.Current.CancellationToken)));
        Assert.Equal(probe.InitialToken, probe.CurrentToken);
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }
    /// <summary>Null input follows the native contract without acquiring a dispatch frame or damaging the active parent.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_NullNativeContext_When_DispatchIsAttempted_Then_NoDispatchLeaseLeaks(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        probe.Request = async context =>
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => probe.DispatchNullAsync().AsTask());
            context.Handle();
        };
        Assert.True(await probe.RunAsync(new(probe.Storage.Clock, TestContext.Current.CancellationToken)));
        Assert.True(probe.Active.Owner.Dispatches.IsIdle);
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }

    /// <summary>A standalone storage owner cannot create a new dispatcher budget outside the shared request boundary.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_StandaloneStorageOwner_When_NativeDispatchBegins_Then_SharedRequestIsRequired(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        await Assert.ThrowsAsync<InvalidOperationException>(probe.DispatchStandaloneAsync);
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }

}
