using System.Globalization;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Retains bounded, allowlisted resource states without serializing configuration or replacing the test failure.</summary>
internal static class ProfileStartupEvidence
{
    private static readonly string[] ResourceNames = ["database", "migrator", "auth", "admin-api", "admin-bff", "worker"];
    private static readonly HashSet<string> SafeStates = new(StringComparer.Ordinal)
    {
        KnownResourceStates.Running, KnownResourceStates.Starting, KnownResourceStates.FailedToStart,
        KnownResourceStates.Exited, KnownResourceStates.Finished, KnownResourceStates.NotStarted,
        KnownResourceStates.Waiting, KnownResourceStates.Stopping, KnownResourceStates.RuntimeUnhealthy
    };
    private static readonly System.Text.CompositeFormat LineFormat = System.Text.CompositeFormat.Parse("profile-resource={0} {1}");
    private static readonly System.Text.CompositeFormat SnapshotFormat = System.Text.CompositeFormat.Parse("snapshot=present state={0} exit={1} pid={2}");
    private const string ProcessIdProperty = "executable.pid";
    private const string Missing = "unavailable";

    /// <summary>Runs the graph checks and snapshots failures before disposal; never retries or changes their cancellation token.</summary>
    internal static Task ObserveAsync(DistributedApplication app, Func<Task> action) => ObserveAsync(action,
        name => app.ResourceNotifications.TryGetCurrentState(name, out var state) ? state.Snapshot : null,
        message => TestContext.Current.TestOutputHelper!.WriteLine(message));

    /// <summary>Preserves the original exception even if snapshot retrieval or the test output sink fails.</summary>
    internal static async Task ObserveAsync(Func<Task> action, Func<string, CustomResourceSnapshot?> read, Action<string> write)
    {
        try
        {
            await action();
        }
        catch
        {
            foreach (var name in ResourceNames)
            {
                try
                {
                    var line = DescribeSafely(name, read);
                    write(line);
                }
                catch (Exception) { /* Diagnostics cannot mask the original test failure. */ }
            }
            throw;
        }
    }

    /// <summary>Maps snapshot access failures to a fixed marker and emits only the caller-owned resource role.</summary>
    private static string DescribeSafely(string name, Func<string, CustomResourceSnapshot?> read)
    {
        const string readFailed = "snapshot-read-failed";
        string description;
        try
        {
            description = Describe(read(name));
        }
        catch (Exception) { description = readFailed; }
        var line = string.Format(CultureInfo.InvariantCulture, LineFormat, name, description);
        return line;
    }

    /// <summary>Formats only allowlisted state text and numeric metadata; arbitrary snapshot values are never converted to strings.</summary>
    internal static string Describe(CustomResourceSnapshot? snapshot)
    {
        const string absent = "snapshot=absent";
        const string unknown = "unknown";
        if (snapshot is null)
        {
            return absent;
        }
        var state = snapshot.State?.Text is { } text && SafeStates.Contains(text) ? text : unknown;
        var exit = snapshot.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? Missing;
        var value = snapshot.Properties.FirstOrDefault(property => property.Name == ProcessIdProperty)?.Value;
        var pid = value is int number && number > 0 ? number.ToString(CultureInfo.InvariantCulture) : Missing;
        var description = string.Format(CultureInfo.InvariantCulture, SnapshotFormat, state, exit, pid);
        return description;
    }
}
