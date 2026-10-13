using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IdentityPlatform.AppHost.Tests.Features;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Owns the diagnostic graph through bounded joining and eventual disposal, including failed startup.</summary>
internal sealed class HistoryStartupGraph
{
    private const string Development = "--environment=Development";
    private const string ProviderPrefix = "--Database:Provider=";
    private const string Database = "database";
    private const string CleanupEvidence = "history-graph cleanup-completed=";
    private const string CleanupFailure = "History diagnostic graph cleanup did not complete successfully.";
    private static readonly string[] Hosts = ["auth", "admin-api", "admin-bff", "worker"];
    private static readonly TimeSpan StartupDeadline = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan CleanupDeadline = TimeSpan.FromSeconds(30);
    private readonly CancellationTokenSource lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private IDistributedApplicationTestingBuilder? builder;
    private DistributedApplication? app;
    private Task? startup;

    /// <summary>Starts the graph only after the incomplete-page control and preserves the observation's primary failure.</summary>
    internal static async Task ObserveAsync(string provider, Func<Func<Task>, Action, Task> observation, HistoryDiagnosticWindow window)
    {
        var graph = new HistoryStartupGraph();
        var cleaned = false;
        try
        {
            graph.lifetime.CancelAfter(StartupDeadline);
            graph.builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
                [Development, ProviderPrefix + provider], graph.lifetime.Token);
            graph.app = await graph.builder.BuildAsync(graph.lifetime.Token);
            await observation(() => graph.BeginAsync(window), window.End);
            Assert.NotNull(graph.startup);
            await graph.startup;
        }
        finally
        {
            // The cleanup task retains ownership if the bounded join expires; disposal never races startup.
            cleaned = await HistoryCleanup.JoinAsync(graph.CleanupAsync(), CleanupDeadline);
            HistoryDiagnosticExperiment.WriteSafely(CleanupEvidence + cleaned);
        }
        Assert.True(cleaned, CleanupFailure);
    }

    /// <summary>Fails promptly if startup terminates before database readiness rather than waiting for the full deadline.</summary>
    private async Task BeginAsync(HistoryDiagnosticWindow window)
    {
        Assert.NotNull(app);
        startup = StartAsync(window);
        var database = app.ResourceNotifications.WaitForResourceAsync(Database, KnownResourceStates.Running, lifetime.Token);
        if (await Task.WhenAny(database, startup) == startup)
        {
            await startup;
        }
        await database;
        window.DatabaseRunning();
        await window.BeginAsync();
    }

    /// <summary>Measures startup through all four hosts' actual healthy states without changing readiness policy.</summary>
    private async Task StartAsync(HistoryDiagnosticWindow window)
    {
        Assert.NotNull(app);
        window.GraphStarted();
        await app.StartAsync(lifetime.Token);
        foreach (var host in Hosts)
        {
            await app.ResourceNotifications.WaitForResourceHealthyAsync(host, WaitBehavior.StopOnResourceUnavailable, lifetime.Token);
        }
        window.GraphReady();
    }

    /// <summary>Retains token, builder and graph ownership until cancellation callbacks and startup terminate, then removes resources.</summary>
    private async Task CleanupAsync()
    {
        try
        {
            await Task.WhenAll(lifetime.CancelAsync(), ObserveStartupFailureAsync());
        }
        finally
        {
            try
            {
                await DisposeGraphAsync();
            }
            finally
            {
                try
                {
                    if (builder is not null)
                    {
                        await builder.DisposeAsync();
                    }
                }
                finally { lifetime.Dispose(); }
            }
        }
    }

    /// <summary>Joins startup without replacing the already propagated startup or browser failure.</summary>
    private async Task ObserveStartupFailureAsync()
    {
        try
        {
            await (startup ?? Task.CompletedTask);
        }
        catch (Exception) { /* The primary observation path owns startup failure reporting. */ }
    }

    /// <summary>Captures synthetic resource identifiers before disposal and verifies their removal within the same cleanup budget.</summary>
    private async Task DisposeGraphAsync()
    {
        if (app is null)
        {
            return;
        }
        LifecycleEvidence? evidence = null;
        try
        {
            evidence = LifecycleEvidence.Capture(app);
        }
        finally { await app.DisposeAsync(); }
        await evidence.AssertRemovedAsync();
    }
}

/// <summary>Bounds joining without abandoning the cleanup task's ownership or exposing cleanup exceptions.</summary>
internal static class HistoryCleanup
{
    /// <summary>Returns false for failed or overdue cleanup while its asynchronous owner continues to retain resources.</summary>
    internal static async Task<bool> JoinAsync(Task cleanup, TimeSpan deadline)
    {
        try
        {
            return await ObserveAsync(cleanup).WaitAsync(deadline);
        }
        catch (Exception) { return false; }
    }

    /// <summary>Observes a delayed failure even after the caller's bounded join has ended.</summary>
    private static async Task<bool> ObserveAsync(Task cleanup)
    {
        try
        {
            await cleanup;
            return true;
        }
        catch (Exception) { return false; }
    }
}
