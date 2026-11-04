using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Exercises genuine consumed-code replay and native durable revocation independently of unrelated real grants.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedTokenReplayTests(BootstrapFixture fixture)
{
    private const string InvalidGrant = "invalid_grant";
    private const string Access = "urn:ietf:params:oauth:token-type:access_token";
    private const string Identity = "urn:ietf:params:oauth:token-type:id_token";
    private const string Revoked = "revoked";
    private const string Valid = "valid";

    /// <summary>Native replay revokes the affected credentials, preserves its authorization as OpenIddict specifies, and leaves a separate real grant intact.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_TwoSuccessfulManagedGrants_When_FirstCodeIsReplayed_Then_OnlyItsLinkedTokensAreDurablyRevoked(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var first = new ManagedTokenFlow(test);
        await first.ApproveAsync();
        var code = await first.CodeAsync();
        using var issued = await first.ExchangeAsync(code);
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var firstAuthorization = Assert.Single((await test.SnapshotAsync()).Interactions).AuthorizationId;
        var second = new ManagedTokenFlow(test);
        await second.ApproveAsync();
        using var unrelated = await second.ExchangeAsync(await second.CodeAsync());
        Assert.Equal(HttpStatusCode.OK, unrelated.StatusCode);
        var before = await test.SnapshotAsync();
        using var replay = await first.ExchangeAsync(code);
        await ManagedTokenEvidence.ErrorAsync(replay, HttpStatusCode.BadRequest, InvalidGrant);
        var after = await test.SnapshotAsync();
        Assert.Equal(before.Tokens.Select(row => row.Id), after.Tokens.Select(row => row.Id));
        Assert.All(after.Tokens.Where(row => row.Authorization?.Id == firstAuthorization && row.Type is Access or Identity), row => Assert.True(row.Status == Revoked));
        Assert.All(after.Tokens.Where(row => row.Authorization?.Id != firstAuthorization && row.Type is Access or Identity), row => Assert.True(row.Status == Valid));
        Assert.True(await ManagedTokenEvidence.AuthorizationStatusAsync(test, firstAuthorization!) == Valid);
        Assert.Equal(before.Authorizations, after.Authorizations);
    }
}
