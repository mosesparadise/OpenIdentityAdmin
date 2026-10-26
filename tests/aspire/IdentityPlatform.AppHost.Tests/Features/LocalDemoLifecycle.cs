using System.Text.Json;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Checks only the run's actual Docker ownership and anonymous volume lifecycle without pruning resources.</summary>
internal static class LocalDemoLifecycle
{
    private const string ListContainers = "ps";
    private const string AllQuiet = "-aq";
    private const string FilterArgument = "--filter";
    private const string RunFilter = "label=openidentityadmin.local-demo.run=";
    private const string InspectArgument = "inspect";
    private const string FormatArgument = "--format";
    private const string MountsProjection = "{{json .Mounts}}";
    private const string MountType = "Type";
    private const string VolumeType = "volume";
    private const string VolumeName = "Name";
    private const string ListVolumes = "ls";
    private const string QuietArgument = "-q";
    private const int CleanupSeconds = 30;

    /// <summary>Records volume identities attached to already verified run-owned containers, retaining no environment values.</summary>
    internal static async Task<string[]> CaptureVolumesAsync(string run, CancellationToken token)
    {
        var containers = await ContainersAsync(run, token);
        Assert.NotEmpty(containers);
        var volumes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var container in containers)
        {
            var output = await LocalDemoRuntime.ExecuteAsync([InspectArgument, FormatArgument, MountsProjection, container], string.Empty, token);
            using var mounts = JsonDocument.Parse(output);
            foreach (var mount in mounts.RootElement.EnumerateArray())
            {
                if (mount.GetProperty(MountType).GetString() == VolumeType)
                {
                    var name = mount.GetProperty(VolumeName).GetString();
                    Assert.False(string.IsNullOrWhiteSpace(name));
                    volumes.Add(name);
                }
            }
        }
        return volumes.ToArray();
    }

    /// <summary>Requires native Aspire/DCP disposal to remove every captured volume and owned container without test-assisted deletion.</summary>
    internal static async Task RequireRemovedAsync(string run, string[] volumes)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
        Assert.Empty(await ContainersAsync(run, deadline.Token));
        var output = await LocalDemoRuntime.ExecuteAsync([VolumeType, ListVolumes, QuietArgument], string.Empty, deadline.Token);
        var remaining = Lines(output).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(volumes, remaining.Contains);
    }

    /// <summary>Uses the generated ownership label to avoid selecting unrelated Docker resources.</summary>
    private static async Task<string[]> ContainersAsync(string run, CancellationToken token) => Lines(
        await LocalDemoRuntime.ExecuteAsync([ListContainers, AllQuiet, FilterArgument, RunFilter + run], string.Empty, token));

    /// <summary>Normalizes native CLI line endings without interpreting values as shell expressions.</summary>
    private static string[] Lines(string output) => output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
