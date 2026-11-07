using System.Net;
using System.Security.Claims;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Checks independent native status and public cryptography before any later replay can conceal late issuance.</summary>
internal static class InitialPilotReplayEvidence
{
    private const string WrongAuthorization = "Winning access token has an unexpected authorization binding.";
    private const string ReusedAuthorization = "The unrelated positive control reused the target authorization.";

    internal const string Failure = "Initial pilot replay completed but a winning native token entry remains usable before any additional replay.";

    /// <summary>Requires both real responses and every winning authorization entry to satisfy the same initial-replay safety oracle.</summary>
    internal static async Task<bool> RequireAsync(BrowserHttpsFixture environment, TestRelyingParty client, TestFlow flow,
        ClaimsPrincipal original, HttpResponseMessage winner, HttpResponseMessage replay)
    {
        await RequireRejectedAsync(replay);
        Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
        using var body = JsonDocument.Parse(await winner.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await client.ValidateAsync(body.RootElement, flow, TestContext.Current.CancellationToken));
        var access = new JsonWebToken(body.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!);
        var authorization = original.GetAuthorizationId()!;
        Assert.True(string.Equals(authorization, access.GetClaim(OpenIddictConstants.Claims.Private.AuthorizationId).Value, StringComparison.Ordinal),
            WrongAuthorization);
        await using var scope = Observer(environment);
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var issued = new Dictionary<string, bool>(StringComparer.Ordinal);
        await foreach (var entry in manager.FindByAuthorizationIdAsync(authorization, TestContext.Current.CancellationToken))
        {
            var id = (await manager.GetIdAsync(entry, TestContext.Current.CancellationToken))!;
            if (id != original.GetTokenId())
            {
                issued.Add(id, await manager.HasStatusAsync(entry, OpenIddictConstants.Statuses.Revoked, TestContext.Current.CancellationToken));
            }
        }
        Assert.True(issued.ContainsKey(access.GetClaim(OpenIddictConstants.Claims.Private.TokenId).Value));
        return issued.Count > 0 && issued.Values.All(value => value);
    }

    /// <summary>Reads committed code state through a fresh observer before the held winning request resumes.</summary>
    internal static async Task RequireCodeAsync(BrowserHttpsFixture environment, string id, bool revoked)
    {
        await using var scope = Observer(environment);
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var code = await manager.FindByIdAsync(id, TestContext.Current.CancellationToken);
        Assert.NotNull(code);
        Assert.True(await manager.HasStatusAsync(code, revoked ? OpenIddictConstants.Statuses.Revoked : OpenIddictConstants.Statuses.Redeemed,
            TestContext.Current.CancellationToken));
    }

    /// <summary>Requires actual native invalid_grant with no access token; callers retain response ownership.</summary>
    internal static async Task RequireRejectedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(OpenIddictConstants.Errors.InvalidGrant, body.RootElement.GetProperty(OpenIddictConstants.Parameters.Error).GetString());
        Assert.False(body.RootElement.TryGetProperty(OpenIddictConstants.Parameters.AccessToken, out _));
    }

    /// <summary>A distinct genuine authorization still exchanges for independently verified, natively valid access.</summary>
    internal static async Task RequireControlAsync(BrowserHttpsFixture environment, TestRelyingParty client, TestFlow control, ClaimsPrincipal original)
    {
        using var response = await client.ExchangeAsync(control.Code, control.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await client.ValidateAsync(body.RootElement, control, TestContext.Current.CancellationToken));
        var access = new JsonWebToken(body.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!);
        Assert.False(string.Equals(original.GetAuthorizationId(), access.GetClaim(OpenIddictConstants.Claims.Private.AuthorizationId).Value, StringComparison.Ordinal),
            ReusedAuthorization);
        await using var scope = Observer(environment);
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var entry = await manager.FindByIdAsync(access.GetClaim(OpenIddictConstants.Claims.Private.TokenId).Value, TestContext.Current.CancellationToken);
        Assert.NotNull(entry);
        Assert.True(await manager.HasStatusAsync(entry, OpenIddictConstants.Statuses.Valid, TestContext.Current.CancellationToken));
    }

    /// <summary>Selects legacy native storage before resolving any observer manager in the mixed host.</summary>
    private static AsyncServiceScope Observer(BrowserHttpsFixture environment)
    {
        var scope = environment.IssuerServices.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.LegacyPilot);
        return scope;
    }
}
