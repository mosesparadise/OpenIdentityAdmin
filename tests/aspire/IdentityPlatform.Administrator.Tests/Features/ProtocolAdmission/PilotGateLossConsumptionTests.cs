using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Targets the stale tracked redemption race against native bulk revocation, which does not change concurrency tokens.</summary>
[Collection("administrator-bootstrap")]
public sealed class PilotGateLossConsumptionTests(BootstrapFixture fixture)
{
    /// <summary>Requires final actual Revoked status whether bulk revocation waits on the Valid lock or exposes an unsafe response-first trace.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_FreshChildHasTrackedValidCode_When_GateIsLostAndNativeBulkRevocationCompetes_Then_RevokedStatusCannotBeRestored(
        string provider, bool standalone)
    {
        var token = TestContext.Current.CancellationToken;
        var probe = new PilotGateLossProbe(PilotGateLossStage.DuringConsumption);
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot, standalonePilot: standalone,
            configureIssuer: probe.Configure, clock: new FactorClock(TimeProvider.System));
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var identity = await pilot.Client.ExpectedIdentity(flow, token);
        var evidence = await PilotGenerationEvidence.BeforeAsync(environment, identity.Item1, token);
        var bulk = await PilotGateLossBulk.BeforeAsync(environment, evidence.CodeId);
        probe.Armed = true;
        var original = pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: token);
        await RequireRaceAsync(environment, provider, evidence, probe, bulk, original);
        probe.Armed = false;
        using var replay = await pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: token);
        await InitialPilotReplayEvidence.RequireRejectedAsync(replay);
        await evidence.RequireReplayedAsync(environment, committed: false, token);
    }

    /// <summary>Always releases and joins both native writers, retaining the first ordering, observer or safety failure.</summary>
    private static async Task RequireRaceAsync(BrowserHttpsFixture environment, string provider, PilotGenerationEvidence evidence,
        PilotGateLossProbe probe, PilotGateLossBulk bulk, Task<HttpResponseMessage> original)
    {
        ExceptionDispatchInfo? failure = null;
        Task? revocation = null;
        try
        {
            await probe.WaitAsync();
            Assert.False(original.IsCompleted);
            await PilotGateLossEvidence.TerminateAsync(environment, probe.Session);
            revocation = bulk.RunAsync();
            var waited = await probe.ObserveBulkAsync(provider, environment.Issuer.Account.Journey.Lease.AdministratorConnection,
                revocation, TestContext.Current.CancellationToken);
            if (waited)
            {
                Assert.False(revocation.IsCompleted || original.IsCompleted);
            }
            else
            {
                await revocation;
                Assert.True(bulk.UnchangedConcurrency);
            }
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        probe.Release();
        await JoinAsync(original, revocation, failure);
        await PilotGateLossEvidence.RequireChainAsync(environment, evidence.CodeId, issued: false);
        await evidence.RequireReplayedAsync(environment, committed: false, TestContext.Current.CancellationToken);
    }

    /// <summary>Checks output only after joining the real original exchange and independent native bulk writer.</summary>
    private static async Task JoinAsync(Task<HttpResponseMessage> original, Task? revocation, ExceptionDispatchInfo? failure)
    {
        try
        {
            using var denied = await original;
            await PilotGateLossEvidence.RequireDeniedAsync(denied, infrastructure: false);
        }
        catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        if (revocation is not null)
        {
            try
            {
                await revocation;
            }
            catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        }
        failure?.Throw();
    }
}
