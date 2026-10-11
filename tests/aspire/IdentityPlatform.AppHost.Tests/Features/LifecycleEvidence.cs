using System.Diagnostics;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.AppHost.Tests.Features;

internal sealed record LifecycleEvidence(string[] Containers, (int Id, DateTime Started)[] Processes)
{
    public LifecycleEvidence Merge(LifecycleEvidence other) => new(
        Containers.Concat(other.Containers).Distinct(StringComparer.Ordinal).ToArray(),
        Processes.Concat(other.Processes).Distinct().ToArray());

    internal static void RequireReplacement((int Id, DateTime Started) original, (int Id, DateTime Started) replacement, bool originalExited) =>
        Assert.True(original.Id > 0 && replacement.Id > 0 && originalExited && original != replacement,
            "Restart requires original process exit and a distinct live process generation.");

    public static LifecycleEvidence Capture(DistributedApplication app)
    {
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var containers = new List<string>();
        var processes = new List<(int, DateTime)>();
        foreach (var resource in model.Resources)
        {
            if (resource is not ContainerResource and not ProjectResource)
            {
                continue;
            }

            Assert.True(app.ResourceNotifications.TryGetCurrentState(resource.Name, out var state),
                $"Missing lifecycle snapshot for {resource.Name}.");
            var properties = state!.Snapshot.Properties;
            if (resource is ContainerResource)
            {
                var id = properties.Single(property => property.Name == "container.id").Value?.ToString();
                Assert.Matches("^[a-f0-9]{64}$", id!);
                containers.Add(id!);
            }
            if (resource is ProjectResource)
            {
                var value = properties.FirstOrDefault(property => property.Name == "executable.pid")?.Value;
                var capturedId = RequireProcessId(state.Snapshot.State?.Text, value);
                if (capturedId is null)
                {
                    continue;
                }

                var id = capturedId.Value;
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
        }
        Assert.NotEmpty(containers);
        return new(containers.ToArray(), processes.ToArray());
    }

    internal static int? RequireProcessId(string? state, object? value)
    {
        if (value is not null)
        {
            var id = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(id > 0, "Invalid project process ID.");
            return id;
        }
        Assert.Contains(state, new[] { KnownResourceStates.Finished, KnownResourceStates.Exited,
            KnownResourceStates.FailedToStart, KnownResourceStates.Waiting, KnownResourceStates.NotStarted });
        return null; // Explicitly terminal or held-before-start, never inferred from absent metadata.
    }

    public async Task AssertRemovedAsync()
    {
        foreach (var id in Containers)
        {
            var start = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var argument in new[] { "container", "ls", "--all", "--quiet", "--no-trunc", "--filter", $"id={id}" })
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
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
        foreach (var (id, started) in Processes)
        {
            try
            {
                using var process = Process.GetProcessById(id);
                Assert.True(process.HasExited || process.StartTime != started, "A captured application process survived disposal.");
            }
            catch (ArgumentException) { /* Captured PID no longer exists. */ }
        }
    }
}
