using System.Diagnostics;
using System.Globalization;
using System.Text;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using IdentityPlatform.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Retains container identities and process generations that must disappear after Aspire disposal.</summary>
/// <param name="Containers">Full container IDs captured from Aspire resource snapshots.</param>
/// <param name="Processes">Process IDs paired with start times so PID reuse does not hide a surviving generation.</param>
/// <param name="Descendants">Optional operating-system generations captured beneath project processes.</param>
internal sealed record LifecycleEvidence(string[] Containers, (int Id, DateTime Started)[] Processes, ProcessGeneration[]? Descendants = null)
{
    private const string ContainerIdProperty = "container.id";
    private const string ProcessIdProperty = "executable.pid";
    private const string FullContainerIdPattern = "^[a-f0-9]{64}$";
    private static readonly CompositeFormat MissingSnapshotMessage = CompositeFormat.Parse("Missing lifecycle snapshot for {0}.");
    private const string InvalidProcessIdMessage = "Invalid project process ID.";
    private const string ReplacementRequiredMessage = "Restart requires original process exit and a distinct live process generation.";
    private const string SurvivingProcessMessage = "A captured application process survived disposal.";
    private const string DockerCommand = "docker";
    private const string ContainerFilterPrefix = "id=";
    private static readonly TimeSpan CleanupDeadline = TimeSpan.FromSeconds(10);
    private static readonly string[] ContainerQueryArguments = ["container", "ls", "--all", "--quiet", "--no-trunc", "--filter"];

    /// <summary>Deduplicate container observations while retaining distinct process generations that reused a PID.</summary>
    public LifecycleEvidence Merge(LifecycleEvidence other) => new(
        Containers.Concat(other.Containers).Distinct(StringComparer.Ordinal).ToArray(),
        Processes.Concat(other.Processes).Distinct().ToArray(),
        (Descendants ?? []).Concat(other.Descendants ?? []).Distinct().ToArray());

    /// <summary>Require a successful health observation strictly newer than the process generation being assessed.</summary>
    internal static bool HasFreshHealth(DateTime processStarted, DateTimeOffset? checkedAt, bool healthy) =>
        healthy && checkedAt > new DateTimeOffset(processStarted.ToUniversalTime());

    /// <summary>Reject a no-op restart, invalid PID or replacement observed before the original generation exited.</summary>
    internal static void RequireReplacement((int Id, DateTime Started) original, (int Id, DateTime Started) replacement, bool originalExited) =>
        Assert.True(original.Id > 0 && replacement.Id > 0 && originalExited && original != replacement, ReplacementRequiredMessage);

    /// <summary>Capture each container and project snapshot, including descendants, before resources are disposed.</summary>
    /// <remarks>Missing snapshots and a graph without containers fail rather than producing empty cleanup evidence.</remarks>
    public static LifecycleEvidence Capture(DistributedApplication app)
    {
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var containers = new List<string>();
        var processes = new List<(int, DateTime)>();
        var descendants = new List<ProcessGeneration>();
        foreach (var resource in model.Resources)
        {
            if (resource is not ContainerResource and not ProjectResource)
            {
                continue;
            }
            Assert.True(app.ResourceNotifications.TryGetCurrentState(resource.Name, out var state),
                string.Format(CultureInfo.InvariantCulture, MissingSnapshotMessage, resource.Name));
            if (resource is ContainerResource)
            {
                containers.Add(CaptureContainer(state!.Snapshot));
            }
            if (resource is ProjectResource)
            {
                CaptureProject(state!.Snapshot, processes, descendants);
            }
        }
        Assert.NotEmpty(containers);
        return new(containers.ToArray(), processes.ToArray(), descendants.Distinct().ToArray());
    }

    /// <summary>Require an unambiguous full Docker container ID from the current resource snapshot.</summary>
    private static string CaptureContainer(CustomResourceSnapshot snapshot)
    {
        var id = snapshot.Properties.Single(property => property.Name == ContainerIdProperty).Value?.ToString();
        Assert.Matches(FullContainerIdPattern, id!);
        return id!;
    }

    /// <summary>Capture descendants before observing the parent generation, tolerating only an already-exited parent race.</summary>
    private static void CaptureProject(CustomResourceSnapshot snapshot, List<(int, DateTime)> processes, List<ProcessGeneration> descendants)
    {
        var value = snapshot.Properties.FirstOrDefault(property => property.Name == ProcessIdProperty)?.Value;
        var capturedId = RequireProcessId(snapshot.State?.Text, value);
        if (capturedId is null)
        {
            return;
        }
        var id = capturedId.Value;
        descendants.AddRange(ProcessTreeEvidence.Capture(id));
        try
        {
            using var process = Process.GetProcessById(id);
            if (!process.HasExited)
            {
                processes.Add((id, process.StartTime));
            }
        }
        catch (ArgumentException) { /* Exited between snapshot and observation. */ }
    }

    /// <summary>Require a positive PID unless the resource explicitly terminated or has not started.</summary>
    /// <remarks>Missing metadata alone never proves that a potentially running project has exited.</remarks>
    internal static int? RequireProcessId(string? state, object? value)
    {
        if (value is not null)
        {
            var id = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            Assert.True(id > 0, InvalidProcessIdMessage);
            return id;
        }
        Assert.Contains(state, new[] { KnownResourceStates.Finished, KnownResourceStates.Exited,
            KnownResourceStates.FailedToStart, KnownResourceStates.Waiting, KnownResourceStates.NotStarted });
        return null;
    }

    /// <summary>Verify descendant termination, container removal and parent-generation exit after disposal.</summary>
    /// <remarks>Descendant waiting and each Docker query receive their own existing ten-second deadline.</remarks>
    public async Task AssertRemovedAsync()
    {
        using var descendantDeadline = new CancellationTokenSource(CleanupDeadline);
        await ProcessTreeEvidence.WaitForTerminationAsync(Descendants ?? [], descendantDeadline.Token);
        foreach (var id in Containers)
        {
            await AssertContainerRemovedAsync(id);
        }
        foreach (var generation in Processes)
        {
            AssertProcessRemoved(generation);
        }
    }

    /// <summary>Query stopped and running containers using full IDs without invoking a shell.</summary>
    private static ProcessStartInfo CreateContainerQuery(string id)
    {
        var start = new ProcessStartInfo(DockerCommand)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in ContainerQueryArguments)
        {
            start.ArgumentList.Add(argument);
        }
        start.ArgumentList.Add(ContainerFilterPrefix + id);
        return start;
    }

    /// <summary>Require a successful empty Docker listing with both streams drained on success; terminate its query tree on timeout.</summary>
    private static async Task AssertContainerRemovedAsync(string id)
    {
        using var process = Process.Start(CreateContainerQuery(id))!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(CleanupDeadline);
        try
        {
            await process.WaitForExitAsync(deadline.Token);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        Assert.Equal(0, process.ExitCode);
        Assert.Empty((await output).Trim());
        await error;
    }

    /// <summary>Accept an absent/exited process or a reused PID, but reject the captured generation still running.</summary>
    private static void AssertProcessRemoved((int Id, DateTime Started) generation)
    {
        try
        {
            using var process = Process.GetProcessById(generation.Id);
            Assert.True(process.HasExited || process.StartTime != generation.Started, SurvivingProcessMessage);
        }
        catch (ArgumentException) { /* Captured PID no longer exists. */ }
    }
}

/// <summary>Merge captures under one lock so concurrent lifecycle observations retain every generation.</summary>
internal sealed class LifecycleEvidenceRecorder
{
    private readonly Lock gate = new();
    private LifecycleEvidence? evidence;

    /// <summary>Atomically merge the latest capture into all evidence retained for final cleanup verification.</summary>
    public void Add(LifecycleEvidence current)
    {
        lock (gate)
        {
            evidence = evidence is null ? current : evidence.Merge(current);
        }
    }

    /// <summary>Gets the latest merged evidence under the same lock used to record captures.</summary>
    public LifecycleEvidence? Snapshot => ReadSnapshot();

    /// <summary>Read the latest capture while excluding concurrent merges.</summary>
    private LifecycleEvidence? ReadSnapshot()
    {
        lock (gate)
        {
            return evidence;
        }
    }
}

/// <summary>Retain bounded stage/type diagnostics while allowing all remaining cleanup actions to be attempted.</summary>
/// <remarks>Callers supply fixed stage identifiers. Exception messages, data and inner exceptions are never retained.</remarks>
internal sealed class LifecycleFailures
{
    private const int MaximumFailures = 32;
    private const string InvalidatedFailure = "invalidated";
    private const string FailureSummary = "Lifecycle verification failed; cleanup may be incomplete. Stages (first 32): ";
    private const string StageSeparator = ", ";
    private readonly Lock gate = new();
    private readonly List<string> failures = [];

    /// <summary>Record at most the first thirty-two stage/type pairs without retaining exception objects or their contents.</summary>
    public void Record(string stage, Exception? failure = null)
    {
        lock (gate)
        {
            if (failures.Count < MaximumFailures)
            {
                failures.Add($"{stage}:{failure?.GetType().Name ?? InvalidatedFailure}");
            }
        }
    }

    /// <summary>Attempt a cleanup action and retain a sanitized failure so later cleanup actions can still run.</summary>
    public async Task AttemptAsync(string stage, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception failure) { Record(stage, failure); }
    }

    /// <summary>Fail final acceptance when any stage failed, exposing only the bounded sanitized stage/type summary.</summary>
    public void RequireSuccess()
    {
        lock (gate)
        {
            Assert.True(failures.Count == 0, FailureSummary + string.Join(StageSeparator, failures));
        }
    }
}
