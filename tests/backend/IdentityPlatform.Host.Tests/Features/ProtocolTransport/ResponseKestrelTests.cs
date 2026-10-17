using System.Diagnostics;
using System.Net;
using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Host.Tests.Features.ProtocolTransport;

/// <summary>Proves actual transport abort does not detach a request delegate or release its concurrency slot.</summary>
public sealed class ResponseKestrelTests
{
    private const int ClientTimeoutSeconds = 25;
    private const int TestWaitSeconds = 5;
    private const int EarliestAbortSeconds = 8;
    private const int LatestAbortSeconds = 20;

    /// <summary>Exercises both a blocked native operation and deferred rejection publication under the same real ten-second watchdog.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_NoncooperativeWork_When_DeadlineAbortsKestrel_Then_RequestAndSlotRemainOwnedUntilActualCompletion(bool publication)
    {
        await using var probe = new StorageRequestProbe();
        using var slot = new SemaphoreSlim(1, 1);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        HttpContext? active = null;
        await using var app = CreateApplication(probe, slot, entered, release, finished, publication, http => active = http);
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(ClientTimeoutSeconds) };
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var stopwatch = Stopwatch.StartNew();
        var sending = client.GetAsync(address, TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(TestWaitSeconds), TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<HttpRequestException>(() => sending);
            Assert.InRange(stopwatch.Elapsed.TotalSeconds, EarliestAbortSeconds, LatestAbortSeconds);
            Assert.False(finished.Task.IsCompleted);
            Assert.Equal(0, slot.CurrentCount);
            Assert.Equal(publication ? 1 : 0, Assert.Single(probe.Databases).Disposals);
            Assert.Equal(publication, ReferenceEquals(probe.Root, active!.RequestServices));
        }
        finally { release.TrySetResult(); }
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(TestWaitSeconds), TestContext.Current.CancellationToken);
        Assert.Equal(1, slot.CurrentCount);
        Assert.Equal(1, Assert.Single(probe.Databases).Disposals);
    }

    /// <summary>Installs the real Kestrel request delegate while exposing its retained context to the lifecycle assertions.</summary>
    private static WebApplication CreateApplication(StorageRequestProbe probe, SemaphoreSlim slot,
        TaskCompletionSource entered, TaskCompletionSource release, TaskCompletionSource finished, bool publication, Action<HttpContext> observe)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var app = builder.Build();
        app.Run(async http =>
        {
            await slot.WaitAsync(TestContext.Current.CancellationToken);
            observe(http);
            http.RequestServices = probe.Root;
            if (publication)
            {
                http.Response.OnStarting(async () => { entered.SetResult(); await release.Task; });
            }
            try
            {
                await ExecuteAsync(http, probe, publication, entered, release);
            }
            finally { slot.Release(); finished.SetResult(); }
        });
        return app;
    }

    /// <summary>Deliberately ignores cancellation until released, so the test measures transport abort independently of cooperative work.</summary>
    private static Task ExecuteAsync(HttpContext http, StorageRequestProbe probe, bool publication,
        TaskCompletionSource entered, TaskCompletionSource release) =>
        ProtocolHttpBoundary.RunAsync(http, probe.Factory, probe.Clock, async (_, _, _) =>
        {
            if (publication)
            {
                return ProtocolResponseOutcome.NativeError;
            }
            entered.SetResult();
            await release.Task;
            return ProtocolResponseOutcome.ReadOnly;
        });
}
