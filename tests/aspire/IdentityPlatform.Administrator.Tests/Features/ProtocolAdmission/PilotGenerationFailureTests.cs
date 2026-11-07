using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Proves durable code consumption and withheld native output through real INSERT, commit-acknowledgement and scope-disposal failures.</summary>
[Collection("administrator-bootstrap")]
public sealed class PilotGenerationFailureTests(BootstrapFixture fixture)
{
    /// <summary>Each independently reached native failure preserves consumption; a separate replay rejects and a genuinely new flow succeeds.</summary>
    [Theory]
    [InlineData("PostgreSql", false, PilotGenerationFailure.AfterInsert, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, PilotGenerationFailure.AfterInsert, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, PilotGenerationFailure.CommitAcknowledgement, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, PilotGenerationFailure.CommitAcknowledgement, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, PilotGenerationFailure.Disposal, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, PilotGenerationFailure.Disposal, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, PilotGenerationFailure.AfterInsert, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, PilotGenerationFailure.AfterInsert, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, PilotGenerationFailure.CommitAcknowledgement, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, PilotGenerationFailure.CommitAcknowledgement, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, PilotGenerationFailure.Disposal, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, PilotGenerationFailure.Disposal, Traits = ["Provider", "SqlServer"])]
    public async Task Given_DurablyConsumedPilotCode_When_GenerationBoundaryFails_Then_OutputIsWithheldAndCodeCannotBeReused(
        string provider, bool standalone, PilotGenerationFailure mode)
    {
        var fault = new PilotGenerationFault(mode);
        var token = TestContext.Current.CancellationToken;
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot, standalonePilot: standalone,
            configureIssuer: fault.Configure, clock: new FactorClock(TimeProvider.System));
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var identity = await pilot.Client.ExpectedIdentity(flow, token);
        var evidence = await PilotGenerationEvidence.BeforeAsync(environment, identity.Item1, token);
        fault.RequireConsumed = () => evidence.RequireConsumedAsync(environment, token);
        fault.Armed = true;
        using var failed = await pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        await PilotWaitState.UnavailableAsync(failed);
        Assert.False(failed.Headers.Contains(PilotGenerationFault.StagedHeader));
        RequireFault(fault, mode);
        var committed = mode != PilotGenerationFailure.AfterInsert;
        await evidence.RequireOutcomeAsync(environment, fault, committed, token);
        fault.Armed = false;
        using var replay = await pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: token);
        await InitialPilotReplayEvidence.RequireRejectedAsync(replay);
        await evidence.RequireReplayedAsync(environment, committed, token);
        await RequireFreshAsync(environment, pilot.Client);
    }

    /// <summary>A genuinely new code succeeds after the failed request and separate replay, retaining unrelated native availability.</summary>
    private static async Task RequireFreshAsync(BrowserHttpsFixture environment, TestRelyingParty client)
    {
        var token = TestContext.Current.CancellationToken;
        var fresh = await PilotReplayJourney.AuthorizeAsync(environment, client);
        using var accepted = await client.ExchangeAsync(fresh.Code, fresh.Verifier, cancellationToken: token);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var body = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync(token));
        Assert.Empty(await client.ValidateAsync(body.RootElement, fresh, token));
        Assert.True(body.RootElement.TryGetProperty(OpenIddictConstants.Parameters.AccessToken, out _));
    }

    /// <summary>Requires the selected fault's actual INSERT/save/commit/disposal path rather than accepting any unavailable response.</summary>
    private static void RequireFault(PilotGenerationFault fault, PilotGenerationFailure mode)
    {
        var committed = mode != PilotGenerationFailure.AfterInsert;
        Assert.Equal(1, fault.Inserts);
        Assert.Equal(1, fault.Saves);
        Assert.Equal(committed ? 1 : 0, fault.CommitAttempts);
        Assert.Equal(committed ? 1 : 0, fault.Commits);
        Assert.Equal(1, fault.Disposals);
        Assert.Equal(1, fault.Failures);
    }
}
