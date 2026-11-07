using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Features.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Names explicitly seeded historical native state; identifiers remain private and are never formatted in assertion messages.</summary>
internal sealed class PilotReplayHistory(string codeId, string accessId, string authorizationId)
{
    internal string CodeId => codeId;
    internal string AccessId => accessId;
    internal string AuthorizationId => authorizationId;

    /// <summary>Completes a genuine exchange, then deliberately revokes only its native code to seed the historical cleanup state.</summary>
    internal static async Task<PilotReplayHistory> SeedAsync(BrowserHttpsFixture environment, TestRelyingParty client, TestFlow flow, PilotCodeObservation observation)
    {
        var token = TestContext.Current.CancellationToken;
        using var response = await client.ExchangeAsync(flow.Code, flow.Verifier, cancellationToken: token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        Assert.Empty(await client.ValidateAsync(body.RootElement, flow, token));
        Assert.NotNull(observation.Principal);
        var access = new JsonWebToken(body.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!);
        var state = new PilotReplayHistory(observation.Principal.GetTokenId()!, access.GetClaim(OpenIddictConstants.Claims.Private.TokenId).Value,
            access.GetClaim(OpenIddictConstants.Claims.Private.AuthorizationId).Value);
        Assert.Equal(state.AuthorizationId, observation.Principal.GetAuthorizationId());
        await SeedCodeRevocationAsync(environment, state);
        await RequireStatusAsync(environment, state.CodeId, OpenIddictConstants.Statuses.Revoked);
        await RequireStatusAsync(environment, state.AccessId, OpenIddictConstants.Statuses.Valid);
        return state;
    }

    /// <summary>Uses the native manager for the fixture-only status change, leaving every linked access entry untouched.</summary>
    private static async Task SeedCodeRevocationAsync(BrowserHttpsFixture environment, PilotReplayHistory state)
    {
        await using var scope = Observer(environment);
        var token = TestContext.Current.CancellationToken;
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var code = await manager.FindByIdAsync(state.CodeId, token);
        Assert.NotNull(code);
        Assert.True(await manager.HasStatusAsync(code, OpenIddictConstants.Statuses.Redeemed, token));
        Assert.Equal(state.AuthorizationId, await manager.GetAuthorizationIdAsync(code, token));
        Assert.True(await manager.TryRevokeAsync(code, token));
    }

    /// <summary>Reads status from a new native context, without deriving authority from an already tracked entity.</summary>
    internal static async Task RequireStatusAsync(BrowserHttpsFixture environment, string id, string status)
    {
        await using var scope = Observer(environment);
        var token = TestContext.Current.CancellationToken;
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await manager.FindByIdAsync(id, token);
        Assert.NotNull(entry);
        Assert.True(await manager.HasStatusAsync(entry, status, token));
    }

    /// <summary>Hashes every native scalar and association so a failed cleanup cannot hide unrelated changes.</summary>
    internal static async Task<string> DigestAsync(BrowserHttpsFixture environment)
    {
        await using var scope = Observer(environment);
        return await ManagedJourneyEvidence.NativeAsync(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
    }

    /// <summary>Selects explicit legacy storage only in mixed hosts; standalone hosts retain their ordinary native store graph.</summary>
    internal static AsyncServiceScope Observer(BrowserHttpsFixture environment)
    {
        var scope = environment.IssuerServices.CreateAsyncScope();
        scope.ServiceProvider.GetService<ProtocolStorageSelection>()?.Select(ProtocolStorageMode.LegacyPilot);
        return scope;
    }
}
