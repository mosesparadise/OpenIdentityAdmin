using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.ProtocolTransport;

/// <summary>Exercises separate child response buffers and real scope disposal without treating staged data as a protocol issuance permit.</summary>
public sealed class ResponseChildTests
{
    private const string Redirect = "/child-output";
    private const byte RejectedBody = 1;
    private const byte AcceptedBody = 2;

    /// <summary>Callbacks run under the child, while its frozen output reaches the parent only after that child has actually disposed.</summary>
    [Fact]
    public async Task Given_SealedChild_When_CleanupCompletes_Then_OnlyDataReachesParent()
    {
        await using var storage = new StorageRequestProbe(clock: new ControlledProbeClock());
        using var output = new MemoryStream();
        var http = CreateHttp(storage, output);
        var callbacks = 0;
        await ProtocolHttpBoundary.RunAsync(http, storage.Factory, storage.Clock, async (_, parent, token) =>
        {
            var original = http.Features.Get<IHttpResponseFeature>();
            var result = await ProtocolResponseChild.RunAsync(http, storage.Factory, async (scope, response, _) =>
            {
                http.Response.Redirect(Redirect);
                http.Response.OnStarting(() =>
                {
                    callbacks++;
                    Assert.Same(scope.Services, http.RequestServices);
                    Assert.Equal(0, ((StorageProbe.ProbeContext)scope.Owner.Database).Disposals);
                    return Task.CompletedTask;
                });
                await response.SealAsync();
                return true;
            });
            Assert.Same(parent.Services, http.RequestServices);
            Assert.Same(original, http.Features.Get<IHttpResponseFeature>());
            Assert.False(http.Response.HasStarted);
            Assert.Equal(1, storage.Databases[1].Disposals);
            Assert.NotNull(result.Output);
            await result.Output.CopyToParentAsync(token);
            return ProtocolResponseOutcome.ReadOnly;
        });
        Assert.Equal(1, callbacks);
        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
        Assert.Equal(Redirect, http.Response.Headers.Location);
        Assert.Same(storage.Root, http.RequestServices);
    }

    /// <summary>Rejecting a child discards its staged bytes and unstarted callbacks, so a fresh child can supply an independent response.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_RejectedChild_When_FreshContinuationRuns_Then_NoRejectedMaterialSurvives(bool started)
    {
        await using var storage = new StorageRequestProbe(clock: new ControlledProbeClock());
        using var output = new MemoryStream();
        var http = CreateHttp(storage, output);
        var callbacks = 0;
        await ProtocolHttpBoundary.RunAsync(http, storage.Factory, storage.Clock, async (_, _, token) =>
        {
            var rejected = await ProtocolResponseChild.RunAsync(http, storage.Factory, async (_, _, cancellation) =>
            {
                http.Response.Headers.Location = Redirect;
                http.Response.OnStarting(() => { callbacks++; return Task.CompletedTask; });
                if (started)
                {
                    await http.Response.Body.WriteAsync(new byte[] { RejectedBody }, cancellation);
                }
                return false;
            });
            Assert.Null(rejected.Output);
            Assert.False(http.Response.HasStarted);
            Assert.Empty(http.Response.Headers.Location.ToString());
            var fresh = await ProtocolResponseChild.RunAsync(http, storage.Factory, async (_, response, cancellation) =>
            {
                http.Response.StatusCode = StatusCodes.Status400BadRequest;
                await http.Response.Body.WriteAsync(new byte[] { AcceptedBody }, cancellation);
                await response.SealAsync();
                return true;
            });
            await fresh.Output!.CopyToParentAsync(token);
            return ProtocolResponseOutcome.ReadOnly;
        });
        Assert.Equal(started ? 1 : 0, callbacks);
        Assert.Equal(StatusCodes.Status400BadRequest, http.Response.StatusCode);
        Assert.Equal(new byte[] { AcceptedBody }, output.ToArray());
        Assert.Empty(http.Response.Headers.Location.ToString());
        Assert.All(storage.Databases, database => Assert.Equal(1, database.Disposals));
    }

    /// <summary>Copying frozen output twice latches original-request failure even if its caller catches the second rejection.</summary>
    [Fact]
    public async Task Given_FrozenOutput_When_CopiedTwice_Then_RootPublishesNoStagedData()
    {
        await using var storage = new StorageRequestProbe(clock: new ControlledProbeClock());
        using var output = new MemoryStream();
        var http = CreateHttp(storage, output);
        var rejectionObserved = false;
        await ProtocolHttpBoundary.RunAsync(http, storage.Factory, storage.Clock, async (_, _, token) =>
        {
            var result = await ProtocolResponseChild.RunAsync(http, storage.Factory, async (_, response, _) =>
            {
                http.Response.Redirect(Redirect);
                await response.SealAsync();
                return true;
            });
            await result.Output!.CopyToParentAsync(token);
            await Assert.ThrowsAsync<InvalidOperationException>(() => result.Output.CopyToParentAsync(token));
            rejectionObserved = true;
            return ProtocolResponseOutcome.ReadOnly;
        });
        Assert.True(rejectionObserved);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
        Assert.Empty(output.ToArray());
        Assert.Empty(http.Response.Headers.Location.ToString());
    }

    /// <summary>A caller-abandoned seal remains retained through the actual callback and child cleanup before output can return.</summary>
    [Fact]
    public async Task Given_AbandonedSeal_When_CallbackIsBlocked_Then_ChildAndOutputRemainOwned()
    {
        await using var storage = new StorageRequestProbe(clock: new ControlledProbeClock());
        using var output = new MemoryStream();
        var http = CreateHttp(storage, output);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = ProtocolHttpBoundary.RunAsync(http, storage.Factory, storage.Clock, async (_, _, token) =>
        {
            var result = await ProtocolResponseChild.RunAsync(http, storage.Factory, (_, response, _) =>
            {
                http.Response.Redirect(Redirect);
                http.Response.OnStarting(async () => { entered.SetResult(); await release.Task; });
                _ = response.SealAsync();
                return ValueTask.FromResult(true);
            });
            Assert.NotNull(result.Output);
            await result.Output.CopyToParentAsync(token);
            return ProtocolResponseOutcome.ReadOnly;
        });
        try
        {
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            Assert.False(running.IsCompleted);
            Assert.Equal(0, storage.Databases[1].Disposals);
            Assert.Empty(output.ToArray());
        }
        finally { release.TrySetResult(); await running; }
        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
        Assert.All(storage.Databases, database => Assert.Equal(1, database.Disposals));
    }

    /// <summary>Installs a real parent feature graph with a separately observable live output stream.</summary>
    private static DefaultHttpContext CreateHttp(StorageRequestProbe storage, MemoryStream output)
    {
        var http = new DefaultHttpContext { RequestServices = storage.Root };
        http.Response.Body = output;
        return http;
    }

    /// <summary>A sealed success remains withheld if actual child service disposal fails, even when the operation catches that failure.</summary>
    [Fact]
    public async Task Given_SealedChild_When_DisposalFails_Then_OutputIsWithheldAndFeaturesRestored()
    {
        await using var storage = new StorageRequestProbe(services => services.AddScoped<FailingCleanup>(), new ControlledProbeClock());
        using var output = new MemoryStream();
        var http = CreateHttp(storage, output);
        var original = http.Features.Get<IHttpResponseFeature>();
        var restorationObserved = false;
        Exception? failure = null;
        await ProtocolHttpBoundary.RunAsync(http, storage.Factory, storage.Clock, async (_, parent, _) =>
        {
            var parentResponse = http.Features.Get<IHttpResponseFeature>();
            failure = await Record.ExceptionAsync(async () => await ProtocolResponseChild.RunAsync(http, storage.Factory, async (scope, response, _) =>
            {
                scope.Services.GetRequiredService<FailingCleanup>();
                http.Response.Redirect(Redirect);
                await response.SealAsync();
                return true;
            }));
            Assert.Same(parent.Services, http.RequestServices);
            Assert.Same(parentResponse, http.Features.Get<IHttpResponseFeature>());
            restorationObserved = true;
            return ProtocolResponseOutcome.ReadOnly;
        });
        Assert.True(restorationObserved);
        var aggregate = Assert.IsType<AggregateException>(failure);
        Assert.IsType<ArithmeticException>(Assert.Single(aggregate.Flatten().InnerExceptions));
        Assert.Same(original, http.Features.Get<IHttpResponseFeature>());
        Assert.Same(storage.Root, http.RequestServices);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
        Assert.Empty(output.ToArray());
        Assert.Empty(http.Response.Headers.Location.ToString());
    }

    /// <summary>Foreign scope and replaced transport attempts poison publication even when the caller restores transport and catches rejection.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_FrozenOutput_When_CopyOwnershipChanges_Then_CaughtRejectionStillWithholdsOutput(bool foreignScope)
    {
        await using var storage = new StorageRequestProbe(clock: new ControlledProbeClock());
        using var output = new MemoryStream();
        var http = CreateHttp(storage, output);
        var rejectionObserved = false;
        await ProtocolHttpBoundary.RunAsync(http, storage.Factory, storage.Clock, async (_, _, token) =>
        {
            var result = await ProtocolResponseChild.RunAsync(http, storage.Factory, async (_, response, _) =>
            {
                http.Response.Redirect(Redirect);
                await response.SealAsync();
                return true;
            });
            if (foreignScope)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await ProtocolStorageChild.RunAsync(storage.Factory, async (_, cancellation) =>
                {
                    await result.Output!.CopyToParentAsync(cancellation);
                    return true;
                }));
            }
            else
            {
                var original = http.Features.Get<IHttpResponseFeature>();
                http.Features.Set<IHttpResponseFeature>(new HttpResponseFeature());
                try
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => result.Output!.CopyToParentAsync(token));
                }
                finally { http.Features.Set(original); }
            }
            rejectionObserved = true;
            return ProtocolResponseOutcome.ReadOnly;
        });
        Assert.True(rejectionObserved);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
        Assert.Empty(output.ToArray());
        Assert.Empty(http.Response.Headers.Location.ToString());
    }

    /// <summary>Fails real asynchronous DI cleanup after a successful seal, without changing transaction or transport behavior.</summary>
    private sealed class FailingCleanup : IAsyncDisposable
    {
        /// <summary>Surfaces a distinct disposal failure that the owning child must retain.</summary>
        public ValueTask DisposeAsync() => ValueTask.FromException(new ArithmeticException());
    }
}
