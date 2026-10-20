using System.Runtime.ExceptionServices;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Requires exact provider-observed contention before releasing one held native operation and joins every outcome.</summary>
internal static class NativeBlockedRace
{
    /// <summary>Starts the peer only after the real winner lock is held; a provider observer must prove that peer waits on that exact session.</summary>
    internal static async Task<T[]> InvokeAsync<T>(Func<Task<T>> startWinner, Task<int> locked, Func<Task<T>> startPeer,
        Task<int> peerLock, TaskCompletionSource release, Func<int, int, CancellationToken, Task> observe)
    {
        var token = TestContext.Current.CancellationToken;
        var winner = startWinner();
        Task<T>? peer = null;
        Task? waiting = null;
        using var observation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var observed = false;
        var phase = NativeRaceCleanup.Phase.WinnerLock;
        ExceptionDispatchInfo? failure = null;
        try
        {
            await RequireSignalAsync(locked, winner, token);
            phase = NativeRaceCleanup.Phase.PeerLock;
            peer = startPeer();
            await RequireSignalAsync(peerLock, peer, token, winner);
            phase = NativeRaceCleanup.Phase.BlockedPeer;
            waiting = observe(await peerLock, await locked, observation.Token);
            if (await Task.WhenAny(waiting, peer, winner).WaitAsync(token) == waiting)
            {
                await waiting;
                observed = true;
            }
            await NativeRaceCleanup.RequirePendingAsync(winner);
            await NativeRaceCleanup.RequirePendingAsync(peer);
            phase = NativeRaceCleanup.Phase.ReleaseJoin;
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            NativeRaceCleanup.Report(phase, observed, winner, peer);
            await NativeRaceCleanup.CompleteAsync(release, observation, waiting, winner, peer, failure);
        }
        Assert.True(observed);
        return [await winner, await peer!];
    }

    /// <summary>Rejects an early completed invocation instead of waiting indefinitely for an unreachable lock signal.</summary>
    private static async Task RequireSignalAsync(Task signal, Task operation, CancellationToken token, Task? winner = null)
    {
        await Task.WhenAny(signal, operation, winner ?? operation).WaitAsync(token);
        if (winner is not null)
        {
            await NativeRaceCleanup.RequirePendingAsync(winner);
        }
        if (!signal.IsCompleted)
        {
            await operation;
            throw new InvalidOperationException();
        }
    }
}
