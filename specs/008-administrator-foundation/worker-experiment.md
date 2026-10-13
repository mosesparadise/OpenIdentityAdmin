# Worker diagnostic experiment source — 13 October 2026

This is a reproducible, temporary experiment for [the investigation](startup-investigation.md), not an accepted test or a product fix. Use the pinned repository at `8792f8599f0e66766250c22870b5f717cedffde3` with its .NET/Aspire prerequisites. Temporarily save the C# below as `tests/aspire/IdentityPlatform.AppHost.Tests/Features/WorkerIncidentProbe.cs`, build that project, then run the exact filtered command in the investigation record. Remove the file and rebuild when finished. Do not incorporate these three cases into the ordinary acceptance counts.

The source preserves all executed statements from the corrected experiment. Only `RequireUnavailableAsync`'s XML description was corrected afterward to acknowledge its exact-message assertion. The initial failed experiment instead awaited `Exited` for both projects and containers and checked only the exception type. Both executions, including that mistaken assumption, are retained in the investigation record. The deliberately compact exploratory fixture is not proposed as compliant permanent regression code; any permanent diagnostic tests must satisfy the repository's quality rules and independent review.

```csharp
using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Runs a fixed, disposable diagnostic experiment; it is not acceptance evidence for the historical incident.</summary>
public sealed class WorkerIncidentProbe
{
    private const string Worker = "worker";
    private const string Database = "database";
    private const string InvalidConfiguration = "invalid-configuration";
    private const string DatabaseLoss = "database-loss";
    private const string WorkerStop = "worker-stop";
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan Observation = TimeSpan.FromSeconds(2);

    /// <summary>Distinguishes terminal Worker failure from running-but-unhealthy readiness without retrying a case.</summary>
    [Theory]
    [InlineData(InvalidConfiguration)]
    [InlineData(DatabaseLoss)]
    [InlineData(WorkerStop)]
    public async Task Given_ControlledWorkerFault_When_HealthWaitRuns_Then_ObservedFailureMatchesResourceState(string fault)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(Deadline);
        var token = deadline.Token;
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            ["--environment=Development", "--Database:Provider=PostgreSql"], token);
        if (fault == InvalidConfiguration)
        {
            builder.CreateResourceBuilder<ProjectResource>(Worker).WithEnvironment("Database__Provider", "invalid-provider");
        }
        LifecycleEvidence? resources = null;
        await using (var app = await builder.BuildAsync(token))
        {
            await ProfileStartupEvidence.ObserveAsync(app, async () =>
            {
                await app.StartAsync(token);
                if (fault == InvalidConfiguration)
                {
                    await app.ResourceNotifications.WaitForResourceAsync(Worker, KnownResourceStates.Finished, token);
                    Assert.True(app.ResourceNotifications.TryGetCurrentState(Worker, out var terminal));
                    Assert.Equal(2, terminal!.Snapshot.ExitCode);
                    resources = LifecycleEvidence.Capture(app);
                    await RequireUnavailableAsync(app, token);
                }
                else
                {
                    await app.ResourceNotifications.WaitForResourceHealthyAsync(Worker, token);
                    resources = LifecycleEvidence.Capture(app);
                    await StopAsync(app, fault == DatabaseLoss ? Database : Worker, token);
                    if (fault == DatabaseLoss)
                    {
                        await RequireRunningUnhealthyAsync(app, token);
                    }
                    else
                    {
                        await RequireUnavailableAsync(app, token);
                    }
                }
                Assert.True(app.ResourceNotifications.TryGetCurrentState(Worker, out var observed));
                TestContext.Current.TestOutputHelper!.WriteLine("probe=" + fault + " " + ProfileStartupEvidence.Describe(observed!.Snapshot));
            });
        }
        Assert.NotNull(resources);
        await resources.AssertRemovedAsync();
    }

    /// <summary>Stops only the selected disposable resource and awaits the actual terminal state.</summary>
    private static async Task StopAsync(DistributedApplication app, string name, CancellationToken token)
    {
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.Single(value => value.Name == name);
        await app.Services.GetRequiredService<ResourceCommandService>().ExecuteCommandAsync(resource, KnownResourceCommands.StopCommand, token);
        var terminal = resource is ProjectResource ? KnownResourceStates.Finished : KnownResourceStates.Exited;
        await app.ResourceNotifications.WaitForResourceAsync(name, terminal, token);
    }

    /// <summary>Requires the pinned fail-closed exception type and generic message for an unavailable process.</summary>
    private static async Task RequireUnavailableAsync(DistributedApplication app, CancellationToken token)
    {
        var error = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            app.ResourceNotifications.WaitForResourceHealthyAsync(Worker, WaitBehavior.StopOnResourceUnavailable, token));
        Assert.Equal("Stopped waiting for resource 'worker' to become healthy because it failed to start.", error.Message);
    }

    /// <summary>Proves a live process with failed storage readiness remains pending until the experiment cancels its wait.</summary>
    private static async Task RequireRunningUnhealthyAsync(DistributedApplication app, CancellationToken token)
    {
        var observed = await app.ResourceNotifications.WaitForResourceAsync(Worker,
            value => value.Snapshot.State?.Text == KnownResourceStates.Running && value.Snapshot.HealthStatus == HealthStatus.Unhealthy, token);
        using var client = new HttpClient { BaseAddress = app.GetEndpoint(Worker, "https") };
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/alive", token)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health", token)).StatusCode);
        Assert.Null(observed.Snapshot.ExitCode);
        using var waiting = CancellationTokenSource.CreateLinkedTokenSource(token);
        waiting.CancelAfter(Observation);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            app.ResourceNotifications.WaitForResourceHealthyAsync(Worker, WaitBehavior.StopOnResourceUnavailable, waiting.Token));
        Assert.False(token.IsCancellationRequested);
    }
}
```
