using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Exercises actual HTTP extraction and native issuance/replay across independent issuer roots sharing durable databases and credentials.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeIssuerReplayTests(BootstrapFixture fixture)
{
    /// <summary>A fresh client assertion cannot redeem an already exchanged code; native linked-token revocation remains isolated from another grant.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_TwoIssuerRootsWithRealNativeGrants_When_CodeIsReplayedWithFreshAssertion_Then_LinkedAccessIsRevokedAndUnrelatedGrantSurvives(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await using var peer = scenario.CreatePeer(provider);
        Assert.NotSame(scenario.Services, peer.Services);
        Assert.NotSame(scenario.Services.GetRequiredService<IssuerCredentials>(), peer.Services.GetRequiredService<IssuerCredentials>());
        var unrelated = await IssueAsync(scenario, peer.Services);
        var target = await IssueAsync(scenario, peer.Services);
        Assert.True(target.AuthorizationId != unrelated.AuthorizationId && target.AccessId != unrelated.AccessId);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeAccountPersistence.StatusAsync(scenario, unrelated.AccessId));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeAccountPersistence.StatusAsync(scenario, target.AccessId));
        var before = await NativeAccountPersistence.CountAsync(scenario);
        var replay = await NativeAccountFlow.ExchangeAsync(scenario, target.Code, peer.Services);
        NativeAccountDenialTests.RequireJson(replay, OpenIddictConstants.Errors.InvalidGrant);
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(scenario));
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, await NativeAccountPersistence.StatusAsync(scenario, target.AccessId));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeAccountPersistence.StatusAsync(scenario, unrelated.AccessId));
    }

    /// <summary>Native PAR on the first issuer, actual shared-cookie authorization on the peer, and token exchange on the first must all succeed.</summary>
    private static async Task<Grant> IssueAsync(AdministratorProtocolScenario scenario, IServiceProvider peer)
    {
        var reference = await NativeAccountFlow.PushAsync(scenario);
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference, peer));
        var authorized = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        var response = await NativeAccountFlow.ExchangeAsync(scenario, code);
        NativeAccountFlow.RequireSuccess(response, StatusCodes.Status200OK);
        using var json = JsonDocument.Parse(response.Body);
        var accessValue = json.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!;
        var access = await NativeAccountFlow.ValidateAsync(scenario, accessValue, OpenIddictConstants.TokenTypeIdentifiers.AccessToken);
        _ = await NativeIdentityTokenOracle.ValidateAsync(scenario, json.RootElement.GetProperty(OpenIddictConstants.Parameters.IdToken).GetString()!, accessValue);
        Assert.True(access.GetAuthorizationId() == authorized.GetAuthorizationId());
        Assert.True(!string.IsNullOrEmpty(access.GetTokenId()) && !string.IsNullOrEmpty(access.GetAuthorizationId()));
        return new(code, access.GetTokenId()!, access.GetAuthorizationId()!);
    }

    /// <summary>Keeps synthetic credentials in memory without a generated credential-bearing record string representation.</summary>
    private sealed class Grant(string code, string access, string authorization)
    {
        internal string Code => code;
        internal string AccessId => access;
        internal string AuthorizationId => authorization;
    }
}
