using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.Browser;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Proves actual created-client human proof, native issuance, real Basic/S256 exchange and absence of administrator authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedTokenHttpTests(BootstrapFixture fixture)
{
    private const string Redeemed = "redeemed";
    private const string Access = "access_token";
    private const string Identity = "id_token";
    private const string Type = "token_type";
    private const string Expires = "expires_in";
    private const string Scope = "scope";
    private const string Bearer = "Bearer";
    private const string OpenId = "openid";
    private const int TokenCount = 4;
    private const int LifetimeSeconds = 300;

    /// <summary>Checks independent committed artifacts and sends the genuine managed access token to the real protected Admin API.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CreatedManagedClientAndFreshConsent_When_RealCodeAndBasicPkceExchangeComplete_Then_OnlyIdentityCredentialsAreIssued(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        await RequireOriginalCredentialAsync(test);
        var authority = await ManagedHumanEvidence.AuthorityAsync(test);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        Assert.Equal(ManagedLoginState.Approved, Assert.Single((await test.SnapshotAsync()).Interactions).State);
        var code = await flow.CodeAsync();
        var issued = await test.SnapshotAsync();
        var row = Assert.Single(issued.Interactions);
        Assert.Equal(ManagedLoginState.Consumed, row.State);
        Assert.Equal(1, issued.Authorizations);
        Assert.True(issued.Tokens.Single(value => value.Id == row.RequestId).Status == Redeemed);
        Assert.True(issued.Tokens.Single(value => value.Id == row.CodeId).Authorization!.Id == row.AuthorizationId);
        using var response = await flow.ExchangeAsync(code);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        ManagedHumanHttp.NoAuthorityCookie(response);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var body = payload.RootElement;
        Assert.True(body.EnumerateObject().Select(value => value.Name).ToHashSet(StringComparer.Ordinal).SetEquals([Access, Identity, Type, Expires, Scope]));
        Assert.True(body.GetProperty(Type).GetString() == Bearer && body.GetProperty(Scope).GetString() == OpenId && body.GetProperty(Expires).GetInt64() is > 0 and <= LifetimeSeconds);
        await ManagedTokenOracle.ValidateAsync(test, flow, body, row.UserId!);
        var final = await test.SnapshotAsync();
        Assert.Equal(TokenCount, final.Tokens.Length);
        Assert.True(final.Tokens.Single(value => value.Id == row.CodeId).Status == Redeemed);
        await ManagedTokenEvidence.IssuanceAuditAsync(test);
        Assert.Equal(authority, await ManagedHumanEvidence.AuthorityAsync(test));
        using var allowed = await test.Http.DirectGetAsync(ApplicationHttpFixture.ApiCollection);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        test.Http.Access = body.GetProperty(Access).GetString()!;
        using var denied = await test.Http.DirectGetAsync(ApplicationHttpFixture.ApiCollection);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    /// <summary>Characterizes the exact invalid-code credential pair on each real provider and proves it creates no native, human or administrator state.</summary>
    private static async Task RequireOriginalCredentialAsync(ManagedCacheTestContext test)
    {
        var before = await ManagedJourneyEvidence.DigestAsync(test);
        var application = await ApplicationHttpUpdateEvidence.SnapshotAsync(test.Http, test.Id);
        await ApplicationCredentialEvidence.RequireAsync(test.Http.Environment.Profile.Issuer, test.ClientIdentifier, test.Secret, test.CallbackUri);
        Assert.Equal(before, await ManagedJourneyEvidence.DigestAsync(test));
        Assert.Equal(application.Digest, (await ApplicationHttpUpdateEvidence.SnapshotAsync(test.Http, test.Id)).Digest);
    }
}
