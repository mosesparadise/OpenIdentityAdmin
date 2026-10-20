using System.Runtime.ExceptionServices;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Runs two real independent transactions in both commit orders with exact provider-observed blocking and complete cleanup.</summary>
internal static class NativeAuthorityRace
{
    /// <summary>Releases the held winner only after its live peer is independently observed waiting on that exact database session.</summary>
    internal static async Task<NativeAccountResult> InvokeAsync(string provider, AdministratorProtocolScenario scenario,
        NativeAccountAttempt attempt, NativeAuthorityMutation mutation, NativeAuthorityRaceProbe probe, bool issuanceFirst)
    {
        var token = TestContext.Current.CancellationToken;
        using var observation = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task? changed = null;
        Task? waiting = null;
        ExceptionDispatchInfo? failure = null;
        var observed = false;
        var issuing = attempt.InvokeAsync();
        try
        {
            await RequireSignalAsync(probe.Captured.Task, issuing, token);
            if (issuanceFirst)
            {
                probe.ContinueIssuance.TrySetResult();
                await RequireSignalAsync(probe.Committing.Task, issuing, token);
                changed = mutation.InvokeAsync();
            }
            else
            {
                changed = mutation.InvokeAsync();
                await RequireSignalAsync(probe.Committing.Task, changed, token, issuing);
                probe.ContinueIssuance.TrySetResult();
            }
            await RequireSignalAsync(probe.PeerLock.Task, issuanceFirst ? changed : issuing, token, issuanceFirst ? issuing : changed);
            var inspector = new NativeLockObservation(provider, scenario.Account.Journey.Lease.AdministratorConnection);
            waiting = inspector.WaitAsync(await probe.PeerLock.Task, await probe.Committing.Task, observation.Token);
            await Task.WhenAny(waiting, issuing, changed).WaitAsync(token);
            await NativeRaceCleanup.RequirePendingAsync(issuing);
            await NativeRaceCleanup.RequirePendingAsync(changed);
            await waiting;
            observed = true;
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally
        {
            probe.ContinueIssuance.TrySetResult();
            NativeRaceCleanup.Report(NativeRaceCleanup.Phase.BlockedPeer, observed, issuanceFirst ? issuing : changed ?? issuing, issuanceFirst ? changed : issuing);
            await NativeRaceCleanup.CompleteAsync(probe.ReleaseCommit, observation, waiting, issuing, changed, failure);
        }
        Assert.True(observed);
        probe.RequireCommits();
        return await issuing;
    }

    /// <summary>Rejects early operation completion instead of waiting indefinitely for an unreachable native boundary.</summary>
    private static async Task RequireSignalAsync(Task signal, Task operation, CancellationToken token, Task? held = null)
    {
        await Task.WhenAny(signal, operation, held ?? operation).WaitAsync(token);
        if (held is not null)
        {
            await NativeRaceCleanup.RequirePendingAsync(held);
        }
        if (!signal.IsCompletedSuccessfully)
        {
            await operation;
            throw new InvalidOperationException();
        }
    }
}
