using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Uses a common bounded native race to distinguish actual provider exclusion from early committed replay.</summary>
internal static class PilotEarlyReleaseWitness
{
    private const string PrematureWinner = "The held native winner completed before the witness released it.";

    /// <summary>Accepts a bad-state observation only after genuine replay commits revocation while the durably consumed winner is still held.</summary>
    internal static async Task<bool> RunAsync(BrowserHttpsFixture environment, TestRelyingParty client, string provider,
        PilotReplayBarrier barrier, PilotReplayGateProbe gate, TestFlow flow, Task<HttpResponseMessage> winner)
    {
        var token = TestContext.Current.CancellationToken;
        var code = await barrier.WaitAsync();
        await InitialPilotReplayEvidence.RequireCodeAsync(environment, code, revoked: false);
        gate.Begin();
        var stop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observation = gate.ObserveAsync(provider, environment.Issuer.Account.Journey.Lease.AdministratorConnection, stop.Task, token);
        var replay = client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: token);
        ExceptionDispatchInfo? failure = null;
        var bypass = false;
        try
        {
            var first = await Task.WhenAny(replay, observation, winner);
            if (first == winner)
            {
                await winner;
                Assert.Fail(PrematureWinner);
            }
            if (observation.IsCompleted)
            {
                await observation;
            }
            if (first == replay)
            {
                await InitialPilotReplayEvidence.RequireRejectedAsync(await replay);
                await InitialPilotReplayEvidence.RequireCodeAsync(environment, code, revoked: true);
                Assert.False(winner.IsCompleted);
                bypass = true;
            }
            else
            {
                Assert.True(await observation);
                Assert.False(replay.IsCompleted || winner.IsCompleted);
            }
            stop.TrySetResult();
            await observation;
            barrier.Release();
            if (!bypass)
            {
                Assert.True(await InitialPilotReplayEvidence.RequireAsync(environment, client, flow, barrier.Principal, await winner, await replay));
            }
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        barrier.Release();
        await FinishAsync(replay, observation, stop, failure);
        return bypass;
    }

    /// <summary>Joins replay and the observer without masking an earlier provider, timeout or assertion failure.</summary>
    private static async Task FinishAsync(Task<HttpResponseMessage> replay, Task observation, TaskCompletionSource stop, ExceptionDispatchInfo? failure)
    {
        try
        {
            using var response = await replay;
        }
        catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        stop.TrySetResult();
        try
        {
            await observation;
        }
        catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        failure?.Throw();
    }
}
