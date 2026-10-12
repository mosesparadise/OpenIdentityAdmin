using System.Globalization;

namespace IdentityPlatform.Testing;

// Linux/WSL evidence only: never read command lines or environments.
internal sealed record ProcessGeneration(int Id, ulong Started);
internal sealed record ProcessSnapshot(ProcessGeneration Generation, int Parent, int Group, char State);

internal static class ProcessTreeEvidence
{
    internal static ProcessSnapshot Parse(int id, string stat)
    {
        var closing = stat.LastIndexOf(')');
        Assert.True(closing > 0, "Malformed process metadata.");
        var fields = stat[(closing + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(fields.Length >= 20 && fields[0].Length == 1, "Incomplete process metadata.");
        return new(new(id, ulong.Parse(fields[19], CultureInfo.InvariantCulture)),
            int.Parse(fields[1], CultureInfo.InvariantCulture), int.Parse(fields[2], CultureInfo.InvariantCulture), fields[0][0]);
    }

    internal static ProcessSnapshot? Read(int id) => Read(id, () => File.ReadAllText($"/proc/{id}/stat"));

    internal static ProcessSnapshot? Read(int id, Func<string> readStat)
    {
        Assert.True(OperatingSystem.IsLinux(), "Process lifecycle evidence requires Linux or WSL2.");
        try
        {
            return Parse(id, readStat());
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        // Linux can open proc metadata successfully, then return ESRCH if the
        // process exits before read. .NET preserves this errno as HResult 3.
        catch (IOException failure) when (failure.HResult == 3) { return null; }
    }

    internal static bool Terminated(ProcessGeneration original, ProcessSnapshot? current) =>
        current is null || current.Generation != original || current.State is 'Z' or 'X';

    internal static bool Terminated(ProcessGeneration original) => Terminated(original, Read(original.Id));

    internal static ProcessGeneration[] Capture(int root)
    {
        var snapshots = new List<ProcessSnapshot>();
        foreach (var directory in Directory.EnumerateDirectories("/proc"))
        {
            if (int.TryParse(Path.GetFileName(directory), out var id) && Read(id) is { } state)
            {
                snapshots.Add(state);
            }
        }
        var owned = new HashSet<int> { root };
        var ownGroup = snapshots.Any(value => value.Generation.Id == root && value.Group == root);
        bool changed;
        do
        {
            changed = false;
            foreach (var state in snapshots.Where(value => owned.Contains(value.Parent) || (ownGroup && value.Group == root)))
            {
                changed |= owned.Add(state.Generation.Id);
            }
        } while (changed);
        return snapshots.Where(value => owned.Contains(value.Generation.Id) && value.State is not 'Z' and not 'X')
            .Select(value => value.Generation).ToArray();
    }

    internal static ProcessGeneration RequireExecutable(IEnumerable<ProcessGeneration> processes, string executable)
    {
        var matches = processes.Where(value => !Terminated(value) &&
            File.ResolveLinkTarget($"/proc/{value.Id}/exe", returnFinalTarget: true)?.Name == executable).ToArray();
        Assert.True(matches.Length == 1, "Expected exactly one live application executable in the captured process tree.");
        return matches[0];
    }

    internal static async Task WaitForTerminationAsync(ProcessGeneration[] processes, CancellationToken cancellationToken)
    {
        while (!processes.All(Terminated))
        {
            await Task.Delay(25, cancellationToken);
        }
    }
}
