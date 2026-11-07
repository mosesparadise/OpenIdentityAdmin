using System.Net;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Uses actual HTTPS and native persistence/disposal to prove exact original five-second generation and ten-second request boundaries.</summary>
[Collection("administrator-bootstrap")]
public sealed class PilotDeadlineTests(BootstrapFixture fixture)
{
    private const int ValidationAndGenerationGates = 2;
    /// <summary>Exact expiry after access INSERT rolls generation back; exact original request expiry after commit/cleanup retains native rows but suppresses output.</summary>
    [Theory]
    [InlineData("PostgreSql", false, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ActualPilotGeneration_When_ExactOriginalBudgetEndsAtSaveOrCleanup_Then_NoSuccessfulResponseEscapes(
        string provider, bool standalone, bool requestExpiry)
    {
        var clock = new PilotDeadlineClock();
        var probe = new PilotDeadlineProbe(clock, requestExpiry);
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot, standalonePilot: standalone,
            configureIssuer: probe.Configure, clock: new FactorClock(TimeProvider.System));
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var identity = await pilot.Client.ExpectedIdentity(flow, TestContext.Current.CancellationToken);
        var rows = await PilotDeadlineRows.BeforeAsync(environment, identity.Item1);
        var token = TestContext.Current.CancellationToken;
        probe.RequireConsumed = () => rows.RequireConsumedAsync(environment, token);
        probe.Armed = true;
        await RequireFailedExchangeAsync(pilot.Client, flow, requestExpiry);
        RequireBoundaries(probe, requestExpiry);
        await rows.RequireAsync(environment, probe, committed: requestExpiry);
        probe.Armed = false;
        using var replay = await pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        await InitialPilotReplayEvidence.RequireRejectedAsync(replay);
        await rows.RequireReplayedAsync(environment, committed: requestExpiry);
        await PilotWaitState.PositiveAsync(environment, pilot.Client);
    }

    /// <summary>A request whose original budget is exhausted aborts real transport; generation-only expiry preserves finite unavailable HTTP output.</summary>
    private static async Task RequireFailedExchangeAsync(TestRelyingParty client, TestFlow flow, bool requestExpiry)
    {
        if (requestExpiry)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => client.ExchangeAsync(flow.Code, flow.Verifier,
                cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(TestContext.Current.CancellationToken.IsCancellationRequested);
        }
        else
        {
            using var response = await client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            await PilotWaitState.UnavailableAsync(response);
        }
    }

    /// <summary>Arbitrary transport/SQL failures cannot satisfy the exact reached native boundary and successful commit/disposal counts.</summary>
    private static void RequireBoundaries(PilotDeadlineProbe probe, bool committed)
    {
        Assert.Equal(ValidationAndGenerationGates, probe.Gates);
        Assert.Equal(1, probe.ValidationReturns);
        Assert.Equal(1, probe.ValidationCommits);
        Assert.Equal(1, probe.ValidationDisposals);
        Assert.Equal(1, probe.Inserts);
        Assert.Equal(1, probe.Saves);
        Assert.Equal(committed ? 1 : 0, probe.Commits);
        Assert.Equal(1, probe.Disposals);
        Assert.Equal(1, probe.Boundaries);
    }
}
