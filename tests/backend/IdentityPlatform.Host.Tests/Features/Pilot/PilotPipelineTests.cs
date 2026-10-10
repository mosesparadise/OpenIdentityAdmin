using System.Diagnostics;
using System.Net;
using IdentityPlatform.AuthServer.Composition;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.Host.Tests.Features.Pilot;

public sealed class PilotPipelineTests
{
    [Fact]
    public async Task Fixed_window_admits_exactly_six_hundred_requests_and_excludes_liveness()
    {
        var entered = 0;
        await using var pipeline = await Pipeline.StartAsync(context =>
        {
            Interlocked.Increment(ref entered);
            context.Response.StatusCode = 204;
            return Task.CompletedTask;
        });
        var elapsed = Stopwatch.StartNew();
        for (var index = 0; index < 600; index++)
        {
            using var accepted = await pipeline.Client.GetAsync("/account/probe", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        }
        using var rejected = await pipeline.Client.GetAsync("/account/probe", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal(600, entered);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(30), "Admission assertions must complete in the original rate window.");
        Assert.Equal("no-store", rejected.Headers.CacheControl?.ToString());
        Assert.True(await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken) ==
            "Too many sign-in requests. Wait a minute and try again.", "Rate denial must use the fixed safe message.");
        using var live = await pipeline.Client.GetAsync("/alive", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
    }

    [Fact]
    public async Task Sixteen_in_flight_requests_reject_the_next_and_release_capacity_after_completion()
    {
        var gates = Enumerable.Range(0, 17).Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        var occupied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readmitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;
        await using var pipeline = await Pipeline.StartAsync(async context =>
        {
            var index = Interlocked.Increment(ref entered) - 1;
            if (index == 15)
            {
                occupied.SetResult();
            }
            if (index == 16)
            {
                readmitted.SetResult();
            }
            await gates[index].Task.WaitAsync(context.RequestAborted);
            context.Response.StatusCode = 204;
        });
        var requests = new List<Task<HttpResponseMessage>>();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            requests.AddRange(Enumerable.Range(0, 16).Select(_ => pipeline.Client.GetAsync("/account/probe", deadline.Token)));
            await occupied.Task.WaitAsync(deadline.Token);
            using var rejected = await pipeline.Client.GetAsync("/account/probe", deadline.Token);
            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.Equal(16, entered);
            using var live = await pipeline.Client.GetAsync("/alive", deadline.Token);
            Assert.Equal(HttpStatusCode.OK, live.StatusCode);
            gates[0].SetResult();
            var completed = await Task.WhenAny(requests).WaitAsync(deadline.Token);
            Assert.Equal(HttpStatusCode.NoContent, (await completed).StatusCode);
            requests.Add(pipeline.Client.GetAsync("/account/probe", deadline.Token));
            await readmitted.Task.WaitAsync(deadline.Token);
            gates[16].SetResult();
            Assert.Equal(HttpStatusCode.NoContent, (await requests[^1]).StatusCode);
        }
        finally
        {
            foreach (var gate in gates)
            {
                gate.TrySetResult();
            }
            try
            {
                await Task.WhenAll(requests);
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested) { }
            finally
            {
                foreach (var request in requests.Where(value => value.IsCompletedSuccessfully))
                {
                    (await request).Dispose();
                }
            }

        }
        Assert.Equal(17, entered);
        Assert.True(requests.Count == 17 && requests.All(value => value.IsCompletedSuccessfully),
            "Every admitted request must complete successfully before cleanup ends.");
        foreach (var request in requests)
        {
            Assert.True((await request).StatusCode == HttpStatusCode.NoContent, "Every admitted request must reach the downstream endpoint.");
        }
    }

    [Fact]
    public async Task Actual_ten_second_timeout_cancels_and_drains_the_downstream_operation()
    {
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var pipeline = await Pipeline.StartAsync(async context =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
            }
            finally { cancelled.TrySetResult(); }
        });
        var elapsed = Stopwatch.StartNew();
        using var response = await pipeline.Client.GetAsync("/account/probe", TestContext.Current.CancellationToken);
        elapsed.Stop();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(cancelled.Task.IsCompletedSuccessfully, "The downstream operation must finish before the timeout response completes.");
        Assert.True(elapsed.Elapsed >= TimeSpan.FromSeconds(9) && elapsed.Elapsed < TimeSpan.FromSeconds(12),
            "The real ten-second cooperative request budget must remain in effect.");
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Client_cancellation_reaches_the_downstream_operation_and_is_drained()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var pipeline = await Pipeline.StartAsync(async context =>
        {
            entered.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
            }
            finally { stopped.TrySetResult(); }
        });
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var request = pipeline.Client.GetAsync("/account/probe", caller.Token);
        try
        {
            await entered.Task.WaitAsync(deadline.Token);
            caller.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { using var response = await request; });
            await stopped.Task.WaitAsync(deadline.Token);
            using var live = await pipeline.Client.GetAsync("/alive", deadline.Token);
            Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        }
        finally
        {
            caller.Cancel();
            try
            {
                try
                {
                    (await request).Dispose();
                }
                catch (OperationCanceledException) when (caller.IsCancellationRequested) { }
            }
            finally
            {
                if (entered.Task.IsCompletedSuccessfully)
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await stopped.Task.WaitAsync(cleanup.Token);
                }
            }
        }
    }

    private sealed class Pipeline : IAsyncDisposable
    {
        private readonly IssuerMaterialFixture material = new();
        private readonly StartupContext startup = new([]);
        private WebApplication? application;
        public HttpClient Client { get; private set; } = null!;

        public static async Task<Pipeline> StartAsync(RequestDelegate probe)
        {
            var pipeline = new Pipeline();
            try
            {
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    ApplicationName = typeof(IssuerCredentials).Assembly.GetName().Name,
                    EnvironmentName = "Development"
                });
                builder.WebHost.UseTestServer();
                builder.Configuration.AddInMemoryCollection(pipeline.material.Settings);
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "PostgreSql",
                    ["ConnectionStrings:platform"] = "Host=localhost;Port=1;Database=synthetic;Username=synthetic;Password=synthetic",
                    ["Pilot:Enabled"] = "true"
                });
                pipeline.application = builder.AddConfigurations().ConfigureServices(pipeline.startup).Build();
                pipeline.application.ConfigureApplication(pipeline.startup);
                pipeline.application.MapGet("/account/probe", probe);
                await pipeline.application.StartAsync(TestContext.Current.CancellationToken);
                pipeline.Client = pipeline.application.GetTestClient();
                pipeline.Client.BaseAddress = pipeline.startup.Credentials!.Issuer;
                pipeline.Client.Timeout = TimeSpan.FromSeconds(15);
                return pipeline;
            }
            catch
            {
                await pipeline.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            Client?.Dispose();
            try
            {
                if (application is not null)
                {
                    await application.DisposeAsync();
                }
            }
            finally
            {
                startup.Dispose();
                material.Dispose();
            }
        }
    }
}
