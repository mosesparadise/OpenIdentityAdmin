using System.Net;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Exercises physical generation-backend loss and real competing requests while an old native continuation remains held.</summary>
[Collection("administrator-bootstrap")]
public sealed class PilotGateLossTests(BootstrapFixture fixture)
{
    /// <summary>Requires actual gate takeover, independently durable native state and no late generation or cleanup from the abandoned request.</summary>
    [Theory]
    [InlineData("PostgreSql", false, PilotGateLossStage.AfterConsumption, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, PilotGateLossStage.AfterConsumption, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, PilotGateLossStage.BeforeConsumption, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, PilotGateLossStage.BeforeConsumption, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, PilotGateLossStage.BeforeRevokedConsumption, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, PilotGateLossStage.BeforeRevokedConsumption, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, PilotGateLossStage.AfterConsumption, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, PilotGateLossStage.AfterConsumption, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, PilotGateLossStage.BeforeConsumption, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, PilotGateLossStage.BeforeConsumption, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, PilotGateLossStage.BeforeRevokedConsumption, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, PilotGateLossStage.BeforeRevokedConsumption, Traits = ["Provider", "SqlServer"])]
    public async Task Given_HeldPilotContinuation_When_GateBackendTerminatesAndContenderCompletes_Then_OldRequestCannotWriteOrPublish(
        string provider, bool standalone, PilotGateLossStage stage)
    {
        var probe = new PilotGateLossProbe(stage);
        var token = TestContext.Current.CancellationToken;
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot, standalonePilot: standalone,
            configureIssuer: probe.Configure, clock: new FactorClock(TimeProvider.System));
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var identity = await pilot.Client.ExpectedIdentity(flow, token);
        var evidence = await PilotGenerationEvidence.BeforeAsync(environment, identity.Item1, token);
        probe.Armed = true;
        var original = pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: token);
        ExceptionDispatchInfo? failure = null;
        try
        {
            await CompeteAsync(environment, pilot.Client, flow, evidence, probe, stage, original);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        probe.Release();
        try
        {
            using var response = await original;
        }
        catch (Exception error) { failure ??= ExceptionDispatchInfo.Capture(error); }
        failure?.Throw();
    }

    /// <summary>Preserves the original deadline and joins the contender before releasing the old request, so no later replay repairs its state.</summary>
    private static async Task CompeteAsync(BrowserHttpsFixture environment, TestRelyingParty client, TestFlow flow,
        PilotGenerationEvidence evidence, PilotGateLossProbe probe, PilotGateLossStage stage, Task<HttpResponseMessage> original)
    {
        var token = TestContext.Current.CancellationToken;
        await probe.WaitAsync();
        Assert.False(original.IsCompleted);
        if (stage == PilotGateLossStage.AfterConsumption)
        {
            await evidence.RequireConsumedAsync(environment, token);
        }
        await PilotGateLossEvidence.TerminateAsync(environment, probe.Session);
        if (stage == PilotGateLossStage.BeforeRevokedConsumption)
        {
            await PilotGateLossEvidence.RevokeWithoutConcurrencyChangeAsync(environment, evidence.CodeId);
        }
        using var contender = await client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: token);
        await probe.RequireContenderAsync();
        var issued = stage == PilotGateLossStage.BeforeConsumption;
        await RequireContenderAsync(client, flow, contender, issued);
        await PilotGateLossEvidence.RequireChainAsync(environment, evidence.CodeId, issued);
        var before = await PilotReplayHistory.DigestAsync(environment);
        Assert.False(original.IsCompleted);
        probe.Release();
        using var denied = await original;
        await PilotGateLossEvidence.RequireDeniedAsync(denied, stage == PilotGateLossStage.AfterConsumption);
        Assert.Equal(before, await PilotReplayHistory.DigestAsync(environment));
        await PilotGateLossEvidence.RequireChainAsync(environment, evidence.CodeId, issued);
    }

    /// <summary>Validates real successful contender cryptography or native replay denial, never accepting a transport-only status assertion.</summary>
    private static async Task RequireContenderAsync(TestRelyingParty client, TestFlow flow, HttpResponseMessage response, bool issued)
    {
        if (!issued)
        {
            await InitialPilotReplayEvidence.RequireRejectedAsync(response);
            return;
        }
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await client.ValidateAsync(body.RootElement, flow, TestContext.Current.CancellationToken));
    }
}
