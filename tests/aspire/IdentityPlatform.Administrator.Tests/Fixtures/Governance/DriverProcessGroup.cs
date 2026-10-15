using System.Runtime.InteropServices;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Retains exact Linux process generations in one observed setsid group and never adopts a new group without a live owned anchor.</summary>
internal sealed class DriverProcessGroup(ProcessGeneration leader)
{
    private const int KillSignal = 9;
    private const int TerminateSignal = 15;
    private const int MissingProcess = 3;
    private const int PollMilliseconds = 25;
    private const string Proc = "/proc";
    private const string OwnershipFailure = "Driver process-group ownership could not be established or retained.";
    private const string SignalFailure = "The owned driver process group could not be signaled.";
    private readonly HashSet<ProcessGeneration> retained = [leader];
    private readonly object gate = new();

    /// <summary>Waits only within the supplied deadline until the observed child is actually the dedicated setsid group leader.</summary>
    internal async Task EstablishAsync(CancellationToken token)
    {
        while (true)
        {
            var current = ProcessTreeEvidence.Read(leader.Id);
            Assert.True(current is not null && current.Generation == leader, OwnershipFailure);
            if (current!.Group == leader.Id)
            {
                return;
            }
            await Task.Delay(PollMilliseconds, token).ConfigureAwait(false);
        }
    }

    /// <summary>Retains group members only while an exact already-owned live generation proves this group has not been replaced.</summary>
    internal void Capture()
    {
        lock (gate)
        {
            var current = Members();
            if (current.Length == 0)
            {
                return;
            }
            Assert.True(current.Any(snapshot => retained.Contains(snapshot.Generation)), OwnershipFailure);
            foreach (var snapshot in current)
            {
                retained.Add(snapshot.Generation);
            }
        }
    }

    /// <summary>Signals the exact group even after its leader exited, provided a retained descendant still anchors ownership.</summary>
    internal void Kill() => Signal(KillSignal);

    /// <summary>Exercises graceful termination only in fixed synthetic controls; ordinary cleanup uses unconditional group kill.</summary>
    internal void TerminateControl() => Signal(TerminateSignal);

    /// <summary>Requires a genuinely live retained descendant before the fixed control releases its leader or tests hard termination.</summary>
    internal void RequireDescendant()
    {
        Capture();
        lock (gate)
        {
            Assert.True(retained.Any(value => value != leader && !ProcessTreeEvidence.Terminated(value)), OwnershipFailure);
        }
    }

    /// <summary>Joins all retained generations and independently requires the actual group to contain no live process.</summary>
    internal async Task RequireTerminatedAsync(CancellationToken token)
    {
        while (!AllTerminated())
        {
            await Task.Delay(PollMilliseconds, token).ConfigureAwait(false);
        }
    }

    /// <summary>Allows a post-disposal assertion without touching disposed process handles or retiring descendant evidence.</summary>
    internal bool AllTerminated()
    {
        lock (gate)
        {
            return retained.All(ProcessTreeEvidence.Terminated) && Members().Length == 0;
        }
    }

    /// <summary>Rejects ownership ambiguity instead of sending a signal to an unverified numeric process-group reuse.</summary>
    private void Signal(int signal)
    {
        lock (gate)
        {
            var current = Members();
            if (current.Length == 0)
            {
                return;
            }
            Assert.True(current.Any(snapshot => retained.Contains(snapshot.Generation)), OwnershipFailure);
            foreach (var snapshot in current)
            {
                retained.Add(snapshot.Generation);
            }
            var result = KillNative(-leader.Id, signal);
            Assert.True(result == 0 || Marshal.GetLastPInvokeError() == MissingProcess, SignalFailure);
        }
    }

    /// <summary>Reads only bounded process identity metadata, never environment variables or command lines.</summary>
    private ProcessSnapshot[] Members() => Directory.EnumerateDirectories(Proc).Select(Path.GetFileName)
        .Where(name => int.TryParse(name, out _)).Select(name => ProcessTreeEvidence.Read(int.Parse(name!, System.Globalization.CultureInfo.InvariantCulture)))
        .OfType<ProcessSnapshot>().Where(value => value.Group == leader.Id && value.State is not 'Z' and not 'X').ToArray();

    /// <summary>Calls the native group signal primitive; no shell parsing, process arguments or credential text is involved.</summary>
    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int KillNative(int process, int signal);
}
