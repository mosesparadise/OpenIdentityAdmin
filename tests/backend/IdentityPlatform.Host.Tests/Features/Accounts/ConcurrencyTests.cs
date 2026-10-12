using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Host.Tests.Features.Accounts;

/// <summary>Exercises real admission and permit release with the administrator feature alone.</summary>
public sealed class ConcurrencyTests
{
    private const int Capacity = 16;
    private const int DeadlineSeconds = 5;

    /// <summary>Holds every permit, observes denial, then releases one and proves renewed admission.</summary>
    [Fact]
    public async Task Given_AdministratorOnly_When_AllPermitsAreHeld_Then_ExcessIsDeniedAndReleasedCapacityIsReusable()
    {
        var probe = new OccupancyProbe();
        await using var host = await AccountHost.StartAsync(false, true, app => app.MapGet(AccountHost.Probe, probe.InvokeAsync));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(DeadlineSeconds));
        var requests = new List<Task<HttpResponseMessage>>();
        try
        {
            requests.AddRange(Enumerable.Range(0, Capacity).Select(_ => host.Client.GetAsync(AccountHost.Probe, deadline.Token)));
            await probe.Occupied.Task.WaitAsync(deadline.Token);
            using var denied = await host.Client.GetAsync(AccountHost.Probe, deadline.Token);
            Assert.Equal(HttpStatusCode.TooManyRequests, denied.StatusCode);
            Assert.Equal(Capacity, probe.Entered);
            probe.Gates[0].SetResult();
            var finished = await Task.WhenAny(requests).WaitAsync(deadline.Token);
            Assert.Equal(HttpStatusCode.NoContent, (await finished).StatusCode);
            requests.Add(host.Client.GetAsync(AccountHost.Probe, deadline.Token));
            await probe.Readmitted.Task.WaitAsync(deadline.Token);
            Assert.Equal(Capacity + 1, probe.Entered);
        }
        finally
        {
            await DrainAsync(probe, requests, deadline.Token);
        }
        Assert.All(requests, request => Assert.True(request.IsCompletedSuccessfully));
        foreach (var request in requests)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await request).StatusCode);
        }
    }

    /// <summary>Releases every held operation and disposes successful responses even when an assertion fails.</summary>
    private static async Task DrainAsync(OccupancyProbe probe, List<Task<HttpResponseMessage>> requests, CancellationToken cancellationToken)
    {
        foreach (var gate in probe.Gates)
        {
            gate.TrySetResult();
        }
        try
        {
            await Task.WhenAll(requests);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally
        {
            foreach (var request in requests.Where(request => request.IsCompletedSuccessfully))
            {
                (await request).Dispose();
            }
        }
    }

    /// <summary>Signals observed downstream admission instead of relying on scheduling sleeps.</summary>
    private sealed class OccupancyProbe
    {
        internal readonly TaskCompletionSource[] Gates = Enumerable.Range(0, Capacity + 1)
            .Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        internal readonly TaskCompletionSource Occupied = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Readmitted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Entered;

        /// <summary>Holds an admitted request until the test releases its individual permit.</summary>
        internal async Task InvokeAsync(HttpContext context)
        {
            var index = Interlocked.Increment(ref Entered) - 1;
            if (index == Capacity - 1)
            {
                Occupied.TrySetResult();
            }
            if (index == Capacity)
            {
                Readmitted.TrySetResult();
            }
            await Gates[index].Task.WaitAsync(context.RequestAborted);
            context.Response.StatusCode = StatusCodes.Status204NoContent;
        }
    }
}
