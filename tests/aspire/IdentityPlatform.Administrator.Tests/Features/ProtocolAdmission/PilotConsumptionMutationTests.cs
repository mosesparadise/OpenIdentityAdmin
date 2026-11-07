using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Requires the observed native redemption write to remain durably consumed after the actual HTTP exchange.</summary>
[Collection("administrator-bootstrap")]
public sealed class PilotConsumptionMutationTests(BootstrapFixture fixture)
{
    private const string ConsumptionBypass = "pilot-consumption-rollback-bypass";
    private const string OriginalCode = "Native consumption must update the exact genuinely issued code.";

    /// <summary>A completed native Redeemed write cannot be rolled back into a live unconsumed code while generation reports denial.</summary>
    [Theory, Trait("CriticalDecision", "pilot-durable-consumption")]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_GenuineNativeRedemption_When_ConsumptionCompletes_Then_TheOriginalCodeRemainsConsumed(string provider)
    {
        var probe = new PilotConsumptionMutationProbe();
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot,
            configureIssuer: probe.Configure, clock: new FactorClock(TimeProvider.System));
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var code = await PilotMutationFlow.CodeAsync(environment, pilot.Client, flow);
        probe.Armed = true;
        using var response = await pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        probe.Armed = false;
        Assert.Equal(1, probe.Saves);
        Assert.True(string.Equals(code, probe.Identifier, StringComparison.Ordinal), OriginalCode);
        var unconsumed = await PilotMutationFlow.IsUnconsumedAsync(environment, code);
        var bypass = probe.Commits == 0 && probe.Rollbacks == 1 && unconsumed;
        if (bypass)
        {
            await InitialPilotReplayEvidence.RequireRejectedAsync(response);
        }
        else
        {
            Assert.Equal(1, probe.Commits);
            Assert.Equal(0, probe.Rollbacks);
            Assert.False(unconsumed);
            Assert.True(await PilotMutationFlow.IsCommittedSuccessAsync(environment, pilot.Client, flow, response));
        }
        Assert.False(bypass, ConsumptionBypass);
    }
}
