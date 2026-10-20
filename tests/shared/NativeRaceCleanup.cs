using System.Runtime.ExceptionServices;

namespace IdentityPlatform.Testing;

/// <summary>Releases and joins real race operations while preserving the first orchestration failure.</summary>
internal static class NativeRaceCleanup
{
    internal enum Phase
    {
        WinnerLock, PeerLock, BlockedPeer, ReleaseJoin
    }
    private const string FailurePhase = "Race phase={0}; observed={1}; winner={2}; peer={3}";
    private const string AbsentPeer = "not-started";
    private const string EarlyWinner = "The winning operation completed before its release.";

    /// <summary>Stops observation immediately if the held winner completed, preserving its exception when it failed.</summary>
    internal static async Task RequirePendingAsync(Task winner)
    {
        if (winner.IsCompleted)
        {
            await winner;
            throw new InvalidOperationException(EarlyWinner);
        }
    }

    /// <summary>Records only fixed phases, task status enums and an observation flag before release/join.</summary>
    internal static void Report(Phase phase, bool observed, Task winner, Task? peer)
    {
        try
        {
            TestContext.Current.TestOutputHelper?.WriteLine(FailurePhase, phase, observed, winner.Status, peer is null ? AbsentPeer : peer.Status.ToString());
        }
        catch (Exception)
        {
            // Diagnostic output must never prevent release/join or replace the original operation failure.
        }
    }

    /// <summary>Never abandons an operation or lets a later cleanup exception replace the original observation failure.</summary>
    internal static async Task CompleteAsync(TaskCompletionSource release, CancellationTokenSource observation,
        Task? waiting, Task winner, Task? peer, ExceptionDispatchInfo? failure)
    {
        release.TrySetResult();
        try
        {
            await observation.CancelAsync();
        }
        catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        if (waiting is not null)
        {
            try
            {
                await waiting;
            }
            catch (OperationCanceledException) when (observation.IsCancellationRequested) { }
            catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        }
        try
        {
            await Task.WhenAll(winner, peer ?? Task.CompletedTask);
        }
        catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        failure?.Throw();
    }
}
