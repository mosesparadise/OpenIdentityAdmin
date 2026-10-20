using System.Runtime.ExceptionServices;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Observes actual provider lock contention while two independent instances compete for the final native exchange capacity.</summary>
internal static class NativeClientStateRace
{

    /// <summary>Requires the peer database session to be blocked by the exact reserved transaction before releasing the winning native creation.</summary>
    internal static async Task VerifyAsync(IServiceProvider first, IServiceProvider second, NativeLockObservation observer, CancellationToken token)
    {
        var reserved = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstQueries = first.GetRequiredService<NativeClientStateQueries>();
        firstQueries.Locked = reserved;
        firstQueries.Release = release.Task;
        var winner = NativeClientStateQuotas.AttemptAsync(first, token);
        Task<bool>? peer = null;
        Task? waiting = null;
        using var observation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var observed = false;
        var phase = NativeRaceCleanup.Phase.WinnerLock;
        ExceptionDispatchInfo? failure = null;
        try
        {
            await RequireSignalAsync(reserved.Task, winner, token);
            phase = NativeRaceCleanup.Phase.PeerLock;
            var session = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            second.GetRequiredService<NativeClientStateQueries>().BeforeLock = session;
            peer = NativeClientStateQuotas.AttemptAsync(second, token);
            await RequireSignalAsync(session.Task, peer, token, winner);
            phase = NativeRaceCleanup.Phase.BlockedPeer;
            waiting = observer.WaitAsync(await session.Task, await reserved.Task, observation.Token);
            if (await Task.WhenAny(waiting, peer, winner).WaitAsync(token) == waiting)
            {
                await waiting;
                observed = true;
            }
            await NativeRaceCleanup.RequirePendingAsync(winner);
            phase = NativeRaceCleanup.Phase.ReleaseJoin;
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            NativeRaceCleanup.Report(phase, observed, winner, peer);
            await NativeRaceCleanup.CompleteAsync(release, observation, waiting, winner, peer, failure);
        }
        Assert.True(observed);
        Assert.True(await winner);
        Assert.False(await peer!);
    }

    /// <summary>Fails immediately if a competitor exits before reaching its intended database observation boundary.</summary>
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
