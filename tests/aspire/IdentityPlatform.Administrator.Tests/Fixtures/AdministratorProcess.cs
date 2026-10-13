using System.Diagnostics;
using System.Globalization;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IdentityPlatform.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Controls only an Aspire-owned administrator process and observes exact Linux/WSL process generations and fresh health.</summary>
internal static class AdministratorProcess
{
    private const string ProcessId = "executable.pid";
    private const string Executable = "IdentityPlatform.AuthServer";
    private const string Https = "https";
    private const int DeadlineSeconds = 45;

    /// <summary>Captures the resource root and real AuthServer executable without reading command lines, environments or logs.</summary>
    internal static ProcessGeneration[] Capture(DistributedApplication application, string name)
    {
        Assert.True(application.ResourceNotifications.TryGetCurrentState(name, out var state));
        var id = Convert.ToInt32(state!.Snapshot.Properties.Single(property => property.Name == ProcessId).Value, CultureInfo.InvariantCulture);
        var tree = ProcessTreeEvidence.Capture(id);
        _ = ProcessTreeEvidence.RequireExecutable(tree, Executable);
        return tree;
    }

    /// <summary>Kills the actual owned child at a test-observed precommit boundary and waits for that exact generation to terminate.</summary>
    internal static async Task KillAsync(DistributedApplication application, string name)
    {
        var owned = ProcessTreeEvidence.RequireExecutable(Capture(application, name), Executable);
        Assert.False(ProcessTreeEvidence.Terminated(owned));
        using var process = Process.GetProcessById(owned.Id);
        process.Kill(entireProcessTree: true);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(DeadlineSeconds));
        await ProcessTreeEvidence.WaitForTerminationAsync([owned], deadline.Token);
    }

    /// <summary>Restarts only the selected host, requiring a new executable generation, fresh health and unchanged canonical HTTPS endpoint.</summary>
    internal static async Task<ProcessGeneration[]> RestartAsync(DistributedApplication application, string name, Uri issuer)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(DeadlineSeconds));
        var token = deadline.Token;
        var original = Capture(application, name);
        var old = ProcessTreeEvidence.RequireExecutable(original, Executable);
        var commands = application.Services.GetRequiredService<ResourceCommandService>();
        var resource = application.Services.GetRequiredService<DistributedApplicationModel>().Resources.Single(row => row.Name == name);
        Assert.True((await commands.ExecuteCommandAsync(resource, KnownResourceCommands.StopCommand, token)).Success);
        await ProcessTreeEvidence.WaitForTerminationAsync(original, token);
        var startedAfter = DateTime.UtcNow;
        Assert.True((await commands.ExecuteCommandAsync(resource, KnownResourceCommands.StartCommand, token)).Success);
        await application.ResourceNotifications.WaitForResourceAsync(name, change =>
            change.Snapshot.State?.Text == KnownResourceStates.Running &&
            change.Snapshot.Properties.Any(property => property.Name == ProcessId && property.Value is not null) &&
            !change.Snapshot.HealthReports.IsEmpty && change.Snapshot.HealthReports.All(report =>
                report.Status == HealthStatus.Healthy && report.LastRunAt >= startedAfter), token);
        var replacement = Capture(application, name);
        Assert.NotEqual(old, ProcessTreeEvidence.RequireExecutable(replacement, Executable));
        Assert.Equal(issuer, application.GetEndpoint(name, Https));
        return replacement;
    }
}
