using System.Net;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Orders real pilot code redemption, replay revocation and later winning issuance without sleeps, transport emulation or weakened replay assertions.</summary>
[Collection("administrator-bootstrap")]
public sealed class PilotReplayOrderingTests(BootstrapFixture fixture)
{
    private const string RevocationFailure = "Ordered native replay failed: winning access remains unrevoked after replay completed before issuance.";

    /// <summary>A replay response must not leave later-issued access usable; all preceding password, crypto and persisted code-state observations are independent.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RedeemedCodeBeforeWinningIssuance_When_ReplayRevocationCompletesFirst_Then_WinningAccessIsRevoked(string provider)
    {
        var barrier = new PilotReplayBarrier();
        var fault = new PilotReplayCleanupFault();
        await using var pilot = await BrowserPilotClient.CreateAsync();
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot, configureIssuer: services =>
        {
            barrier.Configure(services);
            fault.Configure(services);
        }, clock: new FactorClock(TimeProvider.System));
        var control = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var controlIdentity = pilot.Client.ExpectedIdentity;
        var flow = await PilotReplayJourney.AuthorizeAsync(environment, pilot.Client);
        var targetIdentity = pilot.Client.ExpectedIdentity;
        pilot.Client.ExpectedIdentity = (candidate, cancellation) => ReferenceEquals(candidate, control)
            ? controlIdentity(candidate, cancellation) : targetIdentity(candidate, cancellation);
        barrier.Armed = true;
        var token = TestContext.Current.CancellationToken;
        var winner = pilot.Client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: token);
        ExceptionDispatchInfo? failure = null;
        string? targetAuthorization = null;
        try
        {
            targetAuthorization = await RequireOrderingAsync(environment, pilot.Client, barrier, fault, flow, winner);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        await barrier.CompleteAsync(winner, failure);
        barrier.Armed = false;
        await RequireControlAsync(environment, pilot.Client, control, targetAuthorization!);
    }

    /// <summary>Requires persisted redeemed/revoked ordering before release and the original winning-token revocation oracle after actual replay.</summary>
    private static async Task<string> RequireOrderingAsync(BrowserHttpsFixture environment, TestRelyingParty client, PilotReplayBarrier barrier, PilotReplayCleanupFault fault,
        TestFlow flow, Task<HttpResponseMessage> winner)
    {
        var token = TestContext.Current.CancellationToken;
        var codeId = await barrier.WaitAsync();
        Assert.True(await HasStatusAsync(environment, codeId, OpenIddictConstants.Statuses.Redeemed));
        using var loser = await client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: token);
        await RequireReplayAsync(loser);
        Assert.True(await HasStatusAsync(environment, codeId, OpenIddictConstants.Statuses.Revoked));
        barrier.Release();
        var won = await winner;
        Assert.Equal(HttpStatusCode.OK, won.StatusCode);
        using var tokens = JsonDocument.Parse(await won.Content.ReadAsStringAsync(token));
        Assert.Empty(await client.ValidateAsync(tokens.RootElement, flow, token));
        var access = new JsonWebToken(tokens.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!);
        await PilotReplayGuardEvidence.RequireAsync(environment, barrier.Principal);
        await RequireFailedCleanupAsync(environment, client, flow, fault, access.GetClaim(OpenIddictConstants.Claims.Private.TokenId).Value);
        using var replay = await client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: token);
        await RequireReplayAsync(replay);
        Assert.True(await HasStatusAsync(environment, access.GetClaim(OpenIddictConstants.Claims.Private.TokenId).Value,
            OpenIddictConstants.Statuses.Revoked), RevocationFailure);
        return access.GetClaim(OpenIddictConstants.Claims.Private.AuthorizationId).Value;
    }

    /// <summary>Observes an actual failed native UPDATE, unchanged valid access and native invalid_grant before a separate explicit replay succeeds in cleanup.</summary>
    private static async Task RequireFailedCleanupAsync(BrowserHttpsFixture environment, TestRelyingParty client, TestFlow flow,
        PilotReplayCleanupFault fault, string accessId)
    {
        fault.Armed = true;
        try
        {
            using var denied = await client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
            await RequireReplayAsync(denied);
            Assert.Equal(1, fault.Failures);
            Assert.True(await HasStatusAsync(environment, accessId, OpenIddictConstants.Statuses.Valid));
        }
        finally { fault.Armed = false; }
    }

    /// <summary>A separate genuine pilot authorization survives cleanup and still exchanges for cryptographically valid, natively valid access.</summary>
    private static async Task RequireControlAsync(BrowserHttpsFixture environment, TestRelyingParty client, TestFlow control, string targetAuthorization)
    {
        var token = TestContext.Current.CancellationToken;
        using var response = await client.ExchangeAsync(control.Code, control.Verifier, cancellationToken: token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        Assert.Empty(await client.ValidateAsync(body.RootElement, control, token));
        var access = new JsonWebToken(body.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!);
        Assert.False(string.Equals(targetAuthorization, access.GetClaim(OpenIddictConstants.Claims.Private.AuthorizationId).Value, StringComparison.Ordinal));
        Assert.True(await HasStatusAsync(environment, access.GetClaim(OpenIddictConstants.Claims.Private.TokenId).Value, OpenIddictConstants.Statuses.Valid));
    }

    /// <summary>Requires a real rejected exchange without returned token material; the response remains owned by its caller.</summary>
    private static async Task RequireReplayAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(OpenIddictConstants.Errors.InvalidGrant, body.RootElement.GetProperty(OpenIddictConstants.Parameters.Error).GetString());
        Assert.False(body.RootElement.TryGetProperty(OpenIddictConstants.Parameters.AccessToken, out _));
    }

    /// <summary>Reads native status through a fresh context after each observed stage; token IDs stay in memory and never enter diagnostics.</summary>
    private static async Task<bool> HasStatusAsync(BrowserHttpsFixture environment, string id, string status)
    {
        await using var scope = environment.IssuerServices.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.LegacyPilot);
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await manager.FindByIdAsync(id, TestContext.Current.CancellationToken);
        Assert.NotNull(entry);
        return await manager.HasStatusAsync(entry, status, TestContext.Current.CancellationToken);
    }
}
