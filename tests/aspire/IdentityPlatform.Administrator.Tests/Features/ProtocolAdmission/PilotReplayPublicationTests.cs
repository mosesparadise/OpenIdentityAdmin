using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Proves initial replay cleanup survives native error formatting and response-callback failure on both providers and host compositions.</summary>
[Collection("administrator-bootstrap")]
public sealed class PilotReplayPublicationTests(BootstrapFixture fixture)
{
    private const string SetCookie = "Set-Cookie";

    /// <summary>A genuine successful exchange's first replay commits complete chain revocation before either deliberate publication fault; a new flow still succeeds.</summary>
    [Theory]
    [InlineData("PostgreSql", false, PilotReplayPublicationFailure.Formatting, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, PilotReplayPublicationFailure.Formatting, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, PilotReplayPublicationFailure.StartingCallback, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, PilotReplayPublicationFailure.StartingCallback, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, PilotReplayPublicationFailure.Formatting, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, PilotReplayPublicationFailure.Formatting, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, PilotReplayPublicationFailure.StartingCallback, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, PilotReplayPublicationFailure.StartingCallback, Traits = ["Provider", "SqlServer"])]
    public async Task Given_InitiallyReplayedPilotCode_When_ErrorPublicationFails_Then_CommittedChainRevocationRemainsDurable(
        string provider, bool standalone, PilotReplayPublicationFailure mode)
    {
        var observation = new PilotCodeObservation();
        var fault = new PilotReplayPublicationFault(mode);
        var token = TestContext.Current.CancellationToken;
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot, standalonePilot: standalone,
            configureIssuer: services => { observation.Configure(services); fault.Configure(services); }, clock: new FactorClock(TimeProvider.System));
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var evidence = await PilotReplayPublicationEvidence.CreateAsync(environment, pilot.Client, flow, observation);
        fault.RequireCommitted = current => evidence.RequireCommittedAsync(environment, current);
        fault.Armed = true;
        try
        {
            using var response = await pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: token);
            await RequireSuppressedAsync(response);
            Assert.Equal(1, fault.Reached);
            Assert.Equal(1, fault.Observed);
            Assert.Equal(1, fault.Failures);
            await evidence.RequireRetainedAsync(environment, token);
        }
        finally { fault.Armed = false; }
        await RequireFreshAsync(environment, pilot.Client, evidence.AuthorizationId);
    }

    /// <summary>Rejects successful output, staged headers, cookies and redirects rather than treating any transport failure as sufficient evidence.</summary>
    private static async Task RequireSuppressedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await PilotWaitState.UnavailableAsync(response);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains(SetCookie));
        Assert.False(response.Headers.Contains(PilotReplayPublicationFault.StagedHeader));
        Assert.True(response.Headers.CacheControl?.NoStore == true);
    }

    /// <summary>Independently validates a genuinely new authorization's response and its still-valid native access entry after the publication fault.</summary>
    private static async Task RequireFreshAsync(BrowserHttpsFixture environment, TestRelyingParty client, string revokedAuthorization)
    {
        var token = TestContext.Current.CancellationToken;
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, client);
        using var response = await client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        Assert.Empty(await client.ValidateAsync(body.RootElement, flow, token));
        var access = new JsonWebToken(body.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!);
        Assert.False(string.Equals(access.GetClaim(OpenIddictConstants.Claims.Private.AuthorizationId).Value, revokedAuthorization, StringComparison.Ordinal));
        await PilotReplayHistory.RequireStatusAsync(environment, access.GetClaim(OpenIddictConstants.Claims.Private.TokenId).Value, OpenIddictConstants.Statuses.Valid);
    }
}
