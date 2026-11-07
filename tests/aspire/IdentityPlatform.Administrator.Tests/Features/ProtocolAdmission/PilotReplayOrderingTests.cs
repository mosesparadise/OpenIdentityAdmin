using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Preserves historical later-replay cleanup evidence using explicitly seeded native state, without recreating the prohibited issuance race.</summary>
[Collection("administrator-bootstrap")]
public sealed class PilotReplayOrderingTests(BootstrapFixture fixture)
{
    /// <summary>A seeded revoked code retains valid linked access through a failed cleanup, then real replay revokes it without affecting another authorization.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_SeededRevokedCodeWithValidAccess_When_ReplayCleanupFailsThenSucceeds_Then_OnlyBoundChainIsRevoked(string provider, bool standalone)
    {
        var observation = new PilotCodeObservation();
        var fault = new PilotReplayCleanupFault();
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot, standalonePilot: standalone,
            configureIssuer: services => { observation.Configure(services); fault.Configure(services); }, clock: new FactorClock(TimeProvider.System));
        var control = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var controlIdentity = pilot.Client.ExpectedIdentity;
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var targetIdentity = pilot.Client.ExpectedIdentity;
        pilot.Client.ExpectedIdentity = (candidate, token) => ReferenceEquals(candidate, control)
            ? controlIdentity(candidate, token) : targetIdentity(candidate, token);
        var history = await PilotReplayHistory.SeedAsync(environment, pilot.Client, flow, observation);
        await PilotReplayGuardEvidence.RequireAsync(environment, observation.Principal);
        await RequireFailedCleanupAsync(environment, pilot.Client, flow, fault, history);
        using var replay = await pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        await InitialPilotReplayEvidence.RequireRejectedAsync(replay);
        await PilotReplayHistory.RequireStatusAsync(environment, history.AccessId, OpenIddictConstants.Statuses.Revoked);
        await PilotReplayHistory.RequireStatusAsync(environment, history.CodeId, OpenIddictConstants.Statuses.Revoked);
        await RequireControlAsync(environment, pilot.Client, control, history.AuthorizationId);
    }

    /// <summary>Observes the actual failed native update and a complete unchanged native digest before a separate explicit replay succeeds.</summary>
    private static async Task RequireFailedCleanupAsync(BrowserHttpsFixture environment, TestRelyingParty client, TestFlow flow,
        PilotReplayCleanupFault fault, PilotReplayHistory history)
    {
        var before = await PilotReplayHistory.DigestAsync(environment);
        fault.Armed = true;
        try
        {
            using var denied = await client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
            await InitialPilotReplayEvidence.RequireRejectedAsync(denied);
            Assert.Equal(1, fault.Failures);
            Assert.Equal(before, await PilotReplayHistory.DigestAsync(environment));
            await PilotReplayHistory.RequireStatusAsync(environment, history.AccessId, OpenIddictConstants.Statuses.Valid);
        }
        finally { fault.Armed = false; }
    }

    /// <summary>A separate genuine authorization survives cleanup and still exchanges for independently validated, natively valid access.</summary>
    private static async Task RequireControlAsync(BrowserHttpsFixture environment, TestRelyingParty client, TestFlow control, string targetAuthorization)
    {
        var token = TestContext.Current.CancellationToken;
        using var response = await client.ExchangeAsync(control.Code, control.Verifier, cancellationToken: token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        Assert.Empty(await client.ValidateAsync(body.RootElement, control, token));
        var access = new JsonWebToken(body.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!);
        Assert.False(string.Equals(targetAuthorization, access.GetClaim(OpenIddictConstants.Claims.Private.AuthorizationId).Value, StringComparison.Ordinal));
        await PilotReplayHistory.RequireStatusAsync(environment, access.GetClaim(OpenIddictConstants.Claims.Private.TokenId).Value, OpenIddictConstants.Statuses.Valid);
    }
}
