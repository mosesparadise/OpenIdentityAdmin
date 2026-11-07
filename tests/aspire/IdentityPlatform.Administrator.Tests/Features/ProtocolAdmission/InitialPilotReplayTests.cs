using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Requires the initial completed replay to cover winning issuance without another replay repairing the state.</summary>
[Collection("administrator-bootstrap")]
public sealed class InitialPilotReplayTests(BootstrapFixture fixture)
{
    private const string PrematureWinner = "The held winner completed before release.";

    /// <summary>Uses one common safety oracle on the old response-first trace and the corrected positively observed contention trace.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_DurablyConsumedPilotCode_When_InitialReplayCompetesWithIssuance_Then_NoWinningEntrySurvivesReplay(string provider)
    {
        var barrier = new PilotReplayBarrier();
        var gate = new PilotReplayGateProbe();
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot,
            configureIssuer: services => { barrier.Configure(services); gate.Configure(services); }, clock: new FactorClock(TimeProvider.System));
        var control = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var controlIdentity = pilot.Client.ExpectedIdentity;
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var targetIdentity = pilot.Client.ExpectedIdentity;
        pilot.Client.ExpectedIdentity = (candidate, token) => ReferenceEquals(candidate, control)
            ? controlIdentity(candidate, token) : targetIdentity(candidate, token);
        barrier.Armed = true;
        var winner = pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        ExceptionDispatchInfo? failure = null;
        var safe = false;
        try
        {
            safe = await RequireRaceAsync(environment, pilot.Client, provider, barrier, gate, flow, winner);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        await barrier.CompleteAsync(winner, failure);
        barrier.Armed = false;
        await InitialPilotReplayEvidence.RequireControlAsync(environment, pilot.Client, control, barrier.Principal);
        Assert.True(safe, InitialPilotReplayEvidence.Failure);
    }

    /// <summary>Releases and joins every real exchange and the observer even if an earlier ordering assertion fails.</summary>
    private static async Task<bool> RequireRaceAsync(BrowserHttpsFixture environment, TestRelyingParty client, string provider,
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
        var safe = false;
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
            }
            else
            {
                Assert.True(await observation);
                Assert.False(replay.IsCompleted || winner.IsCompleted);
            }
            stop.TrySetResult();
            await observation;
            barrier.Release();
            safe = await InitialPilotReplayEvidence.RequireAsync(environment, client, flow, barrier.Principal, await winner, await replay);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        barrier.Release();
        await FinishAsync(replay, observation, stop, failure);
        return safe;
    }

    /// <summary>Never turns an earlier observer timeout/query failure into successful cleanup cancellation.</summary>
    private static async Task FinishAsync(Task<HttpResponseMessage> replay, Task observation, TaskCompletionSource stop,
        ExceptionDispatchInfo? failure)
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
