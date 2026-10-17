using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Verifies actual native server/client early returns, nested execution and error cleanup under the enclosing request.</summary>
public sealed class NativeDispatchTests
{
    private const string RequestEvent = "request";
    private const string ErrorEvent = "error";
    /// <summary>The native ASP.NET handler dispatches a separate error event after rejection, before final scope disposal.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_NativeRejection_When_ErrorIsDispatched_Then_SameScopeRemainsAliveUntilFinally(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        List<string> events = [];
        probe.Request = context =>
        {
            events.Add(RequestEvent);
            Assert.Equal(0, Assert.Single(probe.Storage.Databases).Disposals);
            context.Reject();
            return ValueTask.CompletedTask;
        };
        probe.Error = context =>
        {
            events.Add(ErrorEvent);
            Assert.Equal(0, Assert.Single(probe.Storage.Databases).Disposals);
            Assert.NotEqual(probe.InitialToken, context.ReadToken());
            context.Handle();
            return ValueTask.CompletedTask;
        };
        Assert.True(await probe.RunAsync(new(probe.Storage.Clock, TestContext.Current.CancellationToken)));
        Assert.Equal([RequestEvent, ErrorEvent], events);
        Assert.Equal(probe.InitialToken, probe.CurrentToken);
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }

    /// <summary>Preserves handled/skipped outcomes and restores nested token bindings instead of ending the outer request early.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Given_NestedNativeDispatch_When_InnerReturns_Then_OuterTokenAndScopeRemainOwned(bool client, bool skip)
    {
        await using var probe = new NativeDispatchProbe(client);
        CancellationToken outer = default;
        var nested = 0;
        probe.Nested = context =>
        {
            nested++;
            Assert.NotEqual(outer, context.ReadToken());
            Assert.True(context.ReadToken().CanBeCanceled);
            context.Handle();
            return ValueTask.CompletedTask;
        };
        probe.Request = async context =>
        {
            outer = context.ReadToken();
            await probe.DispatchNestedAsync();
            Assert.Equal(outer, context.ReadToken());
            Assert.Equal(0, Assert.Single(probe.Storage.Databases).Disposals);
            if (skip)
            {
                context.Skip();
            }
            else
            {
                context.Handle();
            }
        };
        Assert.Equal(!skip, await probe.RunAsync(new(probe.Storage.Clock, TestContext.Current.CancellationToken)));
        Assert.Equal(1, nested);
        Assert.Equal(probe.InitialToken, probe.CurrentToken);
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }

    /// <summary>Propagates a real native handler exception and still restores tokens and disposes the child exactly once.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ThrowingNativeHandler_When_DispatchFails_Then_FinallyDisposesAndRequestCannotRestart(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        var expected = new ArithmeticException();
        probe.Request = _ => throw expected;
        var request = new ProtocolStorageRequest(probe.Storage.Clock, TestContext.Current.CancellationToken);
        Assert.Same(expected, await Assert.ThrowsAsync<ArithmeticException>(() => probe.RunAsync(request)));
        Assert.Equal(probe.InitialToken, probe.CurrentToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(request));
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }

    /// <summary>A handler cannot erase cancellation by replacing the native transaction's shared token.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_NativeTokenSubstitution_When_HandlerReturns_Then_RequestFailsClosed(bool client)
    {
        await using var probe = new NativeDispatchProbe(client);
        probe.Request = context =>
        {
            context.WriteToken(CancellationToken.None);
            context.Handle();
            return ValueTask.CompletedTask;
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(new(probe.Storage.Clock, TestContext.Current.CancellationToken)));
        Assert.Equal(probe.InitialToken, probe.CurrentToken);
        Assert.Equal(1, Assert.Single(probe.Storage.Databases).Disposals);
    }
}
