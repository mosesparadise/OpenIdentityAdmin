using System.Diagnostics;
using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Demonstrates the actual database exclusion violated by gate omission or premature savepoint release.</summary>
[Collection("administrator-bootstrap")]
public sealed class PilotGateMutationTests(BootstrapFixture fixture)
{
    private const int ObserveSeconds = 2;
    private const string GateBypass = "pilot-gate-bypass";
    private const string EarlyRelease = "pilot-premature-gate-release-bypass";

    /// <summary>A held native gate must prevent real successful issuance until the independent holder releases it.</summary>
    [Theory, Trait("CriticalDecision", "pilot-gate")]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_IndependentGateOwner_When_GenuineExchangeCompetes_Then_NoCredentialPrecedesRelease(string provider)
    {
        var probe = new PilotWaitProbe();
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot,
            configureIssuer: probe.Configure, clock: new FactorClock(TimeProvider.System));
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        await using var gate = await PilotWaitGate.AcquireAsync(environment);
        probe.Armed = true;
        probe.StartedAt = Stopwatch.GetTimestamp();
        var token = TestContext.Current.CancellationToken;
        var pending = pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: token);
        ExceptionDispatchInfo? failure = null;
        var bypass = false;
        try
        {
            var first = await Task.WhenAny(probe.Session.Task, pending).WaitAsync(TimeSpan.FromSeconds(ObserveSeconds), token);
            if (first == pending)
            {
                Assert.True(await PilotMutationFlow.IsCommittedSuccessAsync(environment, pilot.Client, flow, await pending));
                bypass = true;
            }
            else
            {
                await gate.ObserveAsync(environment, provider, probe, pending);
                await gate.ReleaseAsync();
                Assert.True(await PilotMutationFlow.IsCommittedSuccessAsync(environment, pilot.Client, flow, await pending));
            }
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        await gate.CompleteAsync(pending, failure);
        probe.Armed = false;
        await PilotMutationFlow.RequireFreshAsync(environment, pilot.Client);
        Assert.False(bypass, GateBypass);
    }

    /// <summary>Replay cannot commit chain revocation while a durably consumed winner still owns its generation critical section.</summary>
    [Theory, Trait("CriticalDecision", "pilot-premature-gate-release")]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_HeldWinningGeneration_When_InitialReplayCompetes_Then_RevocationWaitsForGeneration(string provider)
    {
        await PilotReplayObservation.RequireOrderingAsync();
        var barrier = new PilotReplayBarrier();
        var gate = new PilotReplayGateProbe();
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot,
            configureIssuer: services => { barrier.Configure(services); gate.Configure(services); }, clock: new FactorClock(TimeProvider.System));
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        barrier.Armed = true;
        var winner = pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        ExceptionDispatchInfo? failure = null;
        var bypass = false;
        try
        {
            bypass = await PilotEarlyReleaseWitness.RunAsync(environment, pilot.Client, provider, barrier, gate, flow, winner);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        await barrier.CompleteAsync(winner, failure);
        barrier.Armed = false;
        await PilotMutationFlow.RequireFreshAsync(environment, pilot.Client);
        Assert.False(bypass, EarlyRelease);
    }
}
