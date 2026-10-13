using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IdentityPlatform.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Distinguishes unavailable Worker processes from live processes with an unavailable database.</summary>
public sealed class WorkerAvailabilityTests
{
    private const string Worker = "worker";
    private const string Database = "database";
    private const string Https = "https";
    private const string Alive = "/alive";
    private const string Health = "/health";
    private const string ProcessId = "executable.pid";
    private const string Development = "--environment=Development";
    private const string ProviderPrefix = "--Database:Provider=";
    private const string ProviderSetting = "Database__Provider";
    private const string InvalidProvider = "invalid-provider";
    private const string ContrastStage = "worker-contrast";
    private const string CaptureStage = "resource-capture";
    private const string AppDisposalStage = "application-disposal";
    private const string BuilderDisposalStage = "builder-disposal";
    private const string RemovalStage = "resource-removal";
    private const string CleanupStage = "cleanup-join";
    private static readonly TimeSpan CaseDeadline = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan ContrastCancellation = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CleanupDeadline = TimeSpan.FromSeconds(30);
    private static readonly string[] OtherHosts = ["auth", "admin-api", "admin-bff"];
    private readonly LifecycleEvidenceRecorder resources = new();
    private readonly LifecycleFailures failures = new();
    private readonly TaskCompletionSource observed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IDistributedApplicationTestingBuilder? builder;
    private DistributedApplication? application;
    private long databaseStoppedAt;

    /// <summary>Requires an invalid Worker configuration to produce the pinned unavailable state, exit and diagnostic record.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_InvalidWorkerConfiguration_When_Started_Then_UnavailableExitAndDiagnosticsAgree(string provider) =>
        RunAsync(provider, configurationFailure: true);

    /// <summary>Requires database loss to preserve the Worker generation and liveness while readiness and its wait fail independently.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_HealthyWorker_When_DatabaseStops_Then_ProcessSurvivesWithUnhealthyReadiness(string provider) =>
        RunAsync(provider, configurationFailure: false);

    /// <summary>Owns startup, observation and independent cleanup; every failure remains an explicit sanitized stage.</summary>
    private async Task RunAsync(string provider, bool configurationFailure)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(CaseDeadline);
        try
        {
            await BuildAsync(provider, configurationFailure, deadline.Token);
            ProfileTransitionLog? captured = null;
            await ProfileTransitionObserver.ObserveAsync(WatchAsync, async log =>
            {
                captured = log;
                await StartAsync(configurationFailure, deadline.Token);
                if (configurationFailure)
                {
                    await RequireUnavailableAsync(deadline.Token);
                }
                else
                {
                    await RequireDatabaseLossAsync(log, deadline.Token);
                }
                await observed.Task.WaitAsync(deadline.Token);
            }, Write);
            RequireDiagnostics(captured!, configurationFailure);
        }
        catch (Exception failure) { failures.Record(ContrastStage, failure); }
        finally
        {
            if (!await JoinCleanupAsync(CleanupAsync(), CleanupDeadline))
            {
                failures.Record(CleanupStage);
            }
        }
        failures.RequireSuccess();
    }

    /// <summary>Builds the normal disposable provider graph, overriding only the Worker's provider for the configuration arm.</summary>
    private async Task BuildAsync(string provider, bool configurationFailure, CancellationToken token)
    {
        builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            [Development, ProviderPrefix + provider], token);
        if (configurationFailure)
        {
            builder.CreateResourceBuilder<ProjectResource>(Worker).WithEnvironment(ProviderSetting, InvalidProvider);
        }
        application = await builder.BuildAsync(token);
    }

    /// <summary>Waits for actual graph readiness or the intended terminal Worker, then captures resources before injecting database loss.</summary>
    private async Task StartAsync(bool configurationFailure, CancellationToken token)
    {
        await application!.StartAsync(token);
        foreach (var host in OtherHosts)
        {
            await application.ResourceNotifications.WaitForResourceHealthyAsync(host, WaitBehavior.StopOnResourceUnavailable, token);
        }
        if (configurationFailure)
        {
            await application.ResourceNotifications.WaitForResourceAsync(Worker, KnownResourceStates.Finished, token);
        }
        else
        {
            await application.ResourceNotifications.WaitForResourceHealthyAsync(Worker, WaitBehavior.StopOnResourceUnavailable, token);
        }
        resources.Add(LifecycleEvidence.Capture(application));
    }

    /// <summary>Signals only after the transition collector consumes a terminal Worker or a post-stop unhealthy Worker snapshot.</summary>
    private async IAsyncEnumerable<ResourceEvent> WatchAsync([EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var change in application!.ResourceNotifications.WatchAsync(token))
        {
            yield return change;
            if (change.Resource.Name == Worker && (change.Snapshot.State?.Text == KnownResourceStates.Finished ||
                IsFreshUnhealthy(change.Snapshot, Interlocked.Read(ref databaseStoppedAt))))
            {
                observed.TrySetResult();
            }
        }
    }

    /// <summary>Requires the exact pinned unavailable exception independently of the native Finished/exit observation.</summary>
    private async Task RequireUnavailableAsync(CancellationToken token)
    {
        const int expectedConfigurationExit = 2;
        const string expectedUnavailable = "Stopped waiting for resource 'worker' to become healthy because it failed to start.";
        var state = CurrentWorker();
        Assert.Equal(KnownResourceStates.Finished, state.State?.Text);
        Assert.Equal(expectedConfigurationExit, state.ExitCode);
        var failure = await Assert.ThrowsAsync<DistributedApplicationException>(() =>
            application!.ResourceNotifications.WaitForResourceHealthyAsync(Worker, WaitBehavior.StopOnResourceUnavailable, token));
        Assert.Equal(expectedUnavailable, failure.Message);
    }

    /// <summary>Separates dependency-health failure from process unavailability using actual HTTPS and a fresh bounded wait.</summary>
    private async Task RequireDatabaseLossAsync(ProfileTransitionLog log, CancellationToken token)
    {
        var generation = CurrentGeneration();
        log.SetPhase(ProfilePhase.DatabaseStop);
        var model = application!.Services.GetRequiredService<DistributedApplicationModel>();
        Assert.True((await application.Services.GetRequiredService<ResourceCommandService>().ExecuteCommandAsync(
            model.Resources.Single(resource => resource.Name == Database), KnownResourceCommands.StopCommand, token)).Success);
        await application.ResourceNotifications.WaitForResourceAsync(Database, KnownResourceStates.Exited, token);
        Interlocked.Exchange(ref databaseStoppedAt, DateTimeOffset.UtcNow.UtcTicks);
        await application.ResourceNotifications.WaitForResourceAsync(Worker,
            state => IsFreshUnhealthy(state.Snapshot, Interlocked.Read(ref databaseStoppedAt)), token);
        log.SetPhase(ProfilePhase.DegradedHealth);
        RequireSameGeneration(generation);
        await RequireHttpHealthAsync(token);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        cancellation.CancelAfter(ContrastCancellation);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => application.ResourceNotifications.WaitForResourceHealthyAsync(
            Worker, WaitBehavior.StopOnResourceUnavailable, cancellation.Token));
        Assert.True(cancellation.IsCancellationRequested);
        Assert.False(token.IsCancellationRequested);
        RequireSameGeneration(generation);
    }

    /// <summary>Requires trusted HTTPS to report live but unready without accepting insecure transport or cached health.</summary>
    private async Task RequireHttpHealthAsync(CancellationToken token)
    {
        using var client = new HttpClient { BaseAddress = application!.GetEndpoint(Worker, Https) };
        Assert.Equal(Uri.UriSchemeHttps, client.BaseAddress!.Scheme);
        using var alive = await client.GetAsync(Alive, token);
        using var health = await client.GetAsync(Health, token);
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
    }

    /// <summary>Reads the current native Worker snapshot, failing rather than accepting missing state.</summary>
    private CustomResourceSnapshot CurrentWorker()
    {
        Assert.True(application!.ResourceNotifications.TryGetCurrentState(Worker, out var state));
        return state!.Snapshot;
    }

    /// <summary>Pairs the live PID with its actual operating-system start time to reject process replacement or PID reuse.</summary>
    private (int Id, DateTime Started, ProcessGeneration Child) CurrentGeneration()
    {
        var state = CurrentWorker();
        Assert.Equal(KnownResourceStates.Running, state.State?.Text);
        Assert.Null(state.ExitCode);
        var id = LifecycleEvidence.RequireProcessId(state.State?.Text,
            state.Properties.Single(property => property.Name == ProcessId).Value)!.Value;
        using var process = Process.GetProcessById(id);
        Assert.False(process.HasExited);
        const string workerExecutable = "IdentityPlatform.Worker";
        var child = ProcessTreeEvidence.RequireExecutable(ProcessTreeEvidence.Capture(id), workerExecutable);
        return (id, process.StartTime, child);
    }

    /// <summary>Rejects process replacement, exit or a readiness recovery after the deliberately persistent database failure.</summary>
    private void RequireSameGeneration((int Id, DateTime Started, ProcessGeneration Child) expected)
    {
        Assert.Equal(expected, CurrentGeneration());
        Assert.Equal(HealthStatus.Unhealthy, CurrentWorker().HealthStatus);
    }

    /// <summary>Retains joined records on successful controls and requires state evidence independent of the selected arm.</summary>
    private static void RequireDiagnostics(ProfileTransitionLog log, bool configurationFailure)
    {
        const string expectedObserver = "observer-failed=False joined=True publication-drain=not-confirmed";
        const string firstPrefix = "profile-first-unavailable role=worker ";
        const string expectedFinished = "state=Finished";
        const string expectedExit = "exit=2";
        const string expectedAvailable = "profile-first-unavailable role=worker not-observed";
        const string lastPrefix = "profile-last-observed role=worker ";
        const string expectedRunning = "state=Running";
        const string expectedUnhealthy = "health=Unhealthy";
        var lines = log.Lines();
        foreach (var line in lines)
        {
            Write(line);
        }
        Assert.Contains(expectedObserver, lines[0], StringComparison.Ordinal);
        var first = Assert.Single(lines, line => line.StartsWith(firstPrefix, StringComparison.Ordinal));
        if (configurationFailure)
        {
            Assert.Contains(expectedFinished, first, StringComparison.Ordinal);
            Assert.Contains(expectedExit, first, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(expectedAvailable, first);
            var last = Assert.Single(lines, line => line.StartsWith(lastPrefix, StringComparison.Ordinal));
            Assert.Contains(expectedRunning, last, StringComparison.Ordinal);
            Assert.Contains(expectedUnhealthy, last, StringComparison.Ordinal);
        }
    }

    /// <summary>Attempts capture, every disposal and removal verification independently, even after startup or assertion failure.</summary>
    private async Task CleanupAsync()
    {
        if (application is not null)
        {
            await failures.AttemptAsync(CaptureStage, () =>
            {
                resources.Add(LifecycleEvidence.Capture(application));
                return Task.CompletedTask;
            });
            await failures.AttemptAsync(AppDisposalStage, async () => await application.DisposeAsync());
        }
        if (builder is not null)
        {
            await failures.AttemptAsync(BuilderDisposalStage, async () => await builder.DisposeAsync());
        }
        await failures.AttemptAsync(RemovalStage, async () =>
        {
            Assert.NotNull(resources.Snapshot);
            await resources.Snapshot.AssertRemovedAsync();
        });
    }

    /// <summary>Rejects missing, cached and nonterminal-health observations at or before completed database stop.</summary>
    private static bool IsFreshUnhealthy(CustomResourceSnapshot snapshot, long stoppedAt) =>
        stoppedAt > 0 && snapshot.State?.Text == KnownResourceStates.Running && snapshot.ExitCode is null &&
        snapshot.HealthStatus == HealthStatus.Unhealthy && snapshot.HealthReports.Any(report =>
            report.Status == HealthStatus.Unhealthy && report.LastRunAt?.ToUniversalTime().Ticks > stoppedAt);

    /// <summary>Reports incomplete cleanup without abandoning its resource-owning asynchronous continuation.</summary>
    private static async Task<bool> JoinCleanupAsync(Task cleanup, TimeSpan deadline)
    {
        try
        {
            return await ObserveCleanupAsync(cleanup).WaitAsync(deadline);
        }
        catch (TimeoutException) { return false; }
    }

    /// <summary>Observes eventual cleanup faults even when the caller's bounded join has already expired.</summary>
    private static async Task<bool> ObserveCleanupAsync(Task cleanup)
    {
        try
        {
            await cleanup;
            return true;
        }
        catch (Exception) { return false; }
    }

    /// <summary>Proves missing, old, equal-time and healthy reports cannot impersonate fresh post-stop unhealthy evidence.</summary>
    [Fact]
    [Trait("Category", "Harness")]
    public void Given_HealthReports_When_StopBoundaryIsApplied_Then_OnlyFreshUnhealthyRunningEvidenceMatches()
    {
        const string projectType = "project";
        const string healthCheckName = "health";
        const int failureExit = 2;
        var boundary = DateTime.UtcNow;
        var snapshot = new CustomResourceSnapshot
        {
            ResourceType = projectType,
            Properties = [],
            State = new(KnownResourceStates.Running, null)
        };
        var report = new HealthReportSnapshot(healthCheckName, HealthStatus.Unhealthy, null, null) { LastRunAt = boundary };
        Assert.False(IsFreshUnhealthy(snapshot, boundary.Ticks));
        Assert.False(IsFreshUnhealthy(snapshot.WithHealthReports([report]), boundary.Ticks));
        Assert.False(IsFreshUnhealthy(snapshot.WithHealthReports([report with { LastRunAt = boundary.AddTicks(-1) }]), boundary.Ticks));
        var fresh = snapshot.WithHealthReports([report with { LastRunAt = boundary.AddTicks(1) }]);
        Assert.True(IsFreshUnhealthy(fresh, boundary.Ticks));
        Assert.False(IsFreshUnhealthy(fresh, 0));
        Assert.False(IsFreshUnhealthy(fresh with
        {
            State = new(KnownResourceStates.Finished, null)
        }, boundary.Ticks));
        Assert.False(IsFreshUnhealthy(fresh with
        {
            ExitCode = failureExit
        }, boundary.Ticks));
        Assert.False(IsFreshUnhealthy(snapshot.WithHealthReports([report with { LastRunAt = null }]), boundary.Ticks));
        Assert.False(IsFreshUnhealthy(snapshot.WithHealthReports([report with { Status = HealthStatus.Healthy, LastRunAt = boundary.AddTicks(1) }]), boundary.Ticks));
    }

    /// <summary>Proves an overdue cleanup is never accepted, while subsequent completion and faults remain observable.</summary>
    [Fact]
    [Trait("Category", "Harness")]
    public async Task Given_PendingCleanup_When_JoinExpires_Then_CompletionAndFaultRemainOwnedWithoutAcceptance()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.False(await JoinCleanupAsync(pending.Task, TimeSpan.Zero));
        pending.SetResult();
        Assert.True(await JoinCleanupAsync(pending.Task, CleanupDeadline));
        var fault = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.False(await JoinCleanupAsync(fault.Task, TimeSpan.Zero));
        fault.SetException(new IOException());
        Assert.False(await JoinCleanupAsync(fault.Task, CleanupDeadline));
    }

    /// <summary>Writes only the existing allowlisted transition format to retained native test output.</summary>
    private static void Write(string line) => TestContext.Current.TestOutputHelper!.WriteLine(line);
}
