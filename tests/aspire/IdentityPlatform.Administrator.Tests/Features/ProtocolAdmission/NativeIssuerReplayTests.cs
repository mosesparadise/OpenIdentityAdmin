using System.Text;
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
    private const string LocalReplayError = "error:invalid_token\nerror_description:The request could not be completed.\n";
    private const string LocalContentType = "text/plain;charset=UTF-8";
    private const string WindowsNewLine = "\r\n";
    private const string UnixNewLine = "\n";

    /// <summary>A consumed genuine PAR reference cannot authorize twice on another root; local denial neither redirects nor revokes the already-issued grant.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ConsumedNativePar_When_AnotherIssuerReplaysIt_Then_LocalDenialPreservesIssuedGrant(string provider)
    {
        await using var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        await using var peer = scenario.CreatePeer(provider);
        var reference = await NativeAccountFlow.PushAsync(scenario);
        var identifier = await NativeAccountPersistence.ReferenceIdAsync(scenario, reference);
        var code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference));
        var principal = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
        var issuedBefore = await NativeAccountPersistence.CountAsync(scenario);
        var access = await NativeMutationGrant.RequireAsync(scenario, await NativeAccountFlow.ExchangeAsync(scenario, code, peer.Services), principal, issuedBefore);
        Assert.True(await NativeAccountPersistence.IsRedeemedAsync(scenario, identifier));
        Assert.Equal(1, await NativeAccountPersistence.AccessTokensAsync(scenario));
        var before = await NativeAccountPersistence.CountAsync(scenario);
        var replay = await NativeAccountFlow.AuthorizeAsync(scenario, reference, peer.Services);
        RequireParReplay(replay);
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(scenario));
        Assert.Equal(1, await NativeAccountPersistence.AccessTokensAsync(scenario));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeAccountPersistence.StatusAsync(scenario, access.GetTokenId()!));
        Assert.True(await NativeAccountPersistence.IsRedeemedAsync(scenario, identifier));
        _ = await NativeAccountFlow.PushAsync(scenario, peer.Services);
    }

    /// <summary>Requires the exact local safe native PAR replay error with no credential redirect or cookie.</summary>
    internal static void RequireParReplay(NativeAccountResult response)
    {
        Assert.True(response.Failure is null, response.Failure?.GetType().FullName + Environment.NewLine + response.Failure?.StackTrace);
        Assert.Equal(StatusCodes.Status400BadRequest, response.Status);
        Assert.False(response.HasCookie);
        Assert.True(response.Location.Length == 0);
        Assert.Equal(LocalContentType, response.ContentType);
        Assert.True(Encoding.UTF8.GetString(response.Body).Replace(WindowsNewLine, UnixNewLine, StringComparison.Ordinal) == LocalReplayError);
    }

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
