using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace IdentityPlatform.Host.Tests.Features.ProtocolTransport;

/// <summary>Proves explicit child response sealing retains actual callbacks and rejects producers after the commit-admission boundary.</summary>
public sealed class ResponseSealTests
{
    private const string Redirect = "/sealed-child-result";
    private const int RecursiveCheckSeconds = 3;

    /// <summary>A header-only native response runs callbacks once before child disposal, and root completion reuses that same seal.</summary>
    [Fact]
    public async Task Given_ChildRedirect_When_SealedBeforeCleanup_Then_CallbacksRetainChildServices()
    {
        await using var storage = new StorageRequestProbe(clock: new ControlledProbeClock());
        using var output = new MemoryStream();
        var http = new DefaultHttpContext { RequestServices = storage.Root };
        http.Response.Body = output;
        var callbacks = 0;
        await ProtocolHttpBoundary.RunAsync(http, storage.Factory, storage.Clock, async (_, _, _) =>
        {
            var previous = http.RequestServices;
            try
            {
                return await ProtocolStorageChild.RunAsync(storage.Factory, async (child, _) =>
                {
                    http.RequestServices = child.Services;
                    http.Response.Redirect(Redirect);
                    http.Response.OnStarting(() =>
                    {
                        callbacks++;
                        Assert.Same(child.Services, http.RequestServices);
                        Assert.Equal(0, ((StorageProbe.ProbeContext)child.Owner.Database).Disposals);
                        return Task.CompletedTask;
                    });
                    await Assert.IsType<ProtocolResponseCapture>(http.Features.Get<IHttpResponseFeature>()).SealAsync();
                    Assert.Equal(1, callbacks);
                    return ProtocolResponseOutcome.ReadOnly;
                });
            }
            finally { http.RequestServices = previous; }
        });
        Assert.Equal(1, callbacks);
        Assert.Equal(StatusCodes.Status302Found, http.Response.StatusCode);
        Assert.Equal(Redirect, http.Response.Headers.Location);
        Assert.All(storage.Databases, database => Assert.Equal(1, database.Disposals));
        Assert.Same(storage.Root, http.RequestServices);
    }

    /// <summary>Even a caught write after sealing invalidates the whole staged response instead of publishing changed or truncated output.</summary>
    [Fact]
    public async Task Given_SealedResponse_When_LateProducerIsCaught_Then_PublicationIsDenied()
    {
        await using var storage = new StorageRequestProbe(clock: new ControlledProbeClock());
        using var output = new MemoryStream();
        var http = new DefaultHttpContext { RequestServices = storage.Root };
        http.Response.Body = output;
        await ProtocolHttpBoundary.RunAsync(http, storage.Factory, storage.Clock, async (_, _, _) =>
        {
            http.Response.Redirect(Redirect);
            var capture = Assert.IsType<ProtocolResponseCapture>(http.Features.Get<IHttpResponseFeature>());
            await capture.SealAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => http.Response.Body.WriteAsync(new byte[] { 1 }, TestContext.Current.CancellationToken).AsTask());
            return ProtocolResponseOutcome.ReadOnly;
        });
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
        Assert.Empty(http.Response.Headers.Location.ToString());
        Assert.Empty(output.ToArray());
    }

    /// <summary>The seal remains incomplete while a real callback is blocked, including concurrent requests to seal the same response.</summary>
    [Fact]
    public async Task Given_BlockedCallback_When_SealIsRequestedTwice_Then_BothWaitForActualCompletion()
    {
        var request = new ProtocolStorageRequest(new ControlledProbeClock(), TestContext.Current.CancellationToken);
        using var capture = new ProtocolResponseCapture(request);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        capture.OnStarting(async _ => { entered.SetResult(); await release.Task; }, new object());
        var first = capture.SealAsync();
        try
        {
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            Assert.Same(first, capture.SealAsync());
            Assert.False(first.IsCompleted);
        }
        finally { release.TrySetResult(); await first; }
        Assert.True(capture.HasStarted);
    }

    /// <summary>A callback cannot await the seal that is waiting for that same callback; rejection remains terminal even when caught.</summary>
    [Fact]
    public async Task Given_StartingCallback_When_ItRecursivelySeals_Then_RejectsWithoutSelfWait()
    {
        var request = new ProtocolStorageRequest(new ControlledProbeClock(), TestContext.Current.CancellationToken);
        using var capture = new ProtocolResponseCapture(request);
        capture.OnStarting(async _ => await Assert.ThrowsAsync<InvalidOperationException>(() => capture.SealAsync()), new object());
        await Assert.ThrowsAsync<InvalidOperationException>(() => capture.SealAsync())
            .WaitAsync(TimeSpan.FromSeconds(RecursiveCheckSeconds), TestContext.Current.CancellationToken);
        Assert.Throws<InvalidOperationException>(() => request.RequirePublication(ProtocolResponseOutcome.ReadOnly));
    }
}
