using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Proves profile data cannot escape actual late formatter/provider/cleanup failures on either supported provider.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedUserInfoPublicationTests(BootstrapFixture fixture)
{
    private const string UnapprovedHeader = "X-Unapproved-Profile";
    private const string InjectedValue = "synthetic";
    private const string PublicationBypass = "managed-userinfo-publication-bypass";
    private const string Requested = "email openid";
    private const string AccessField = "access_token";

    /// <summary>Current consent does not permit publication after a changed callback, failed acknowledgement/disposal or expired original deadline.</summary>
    [Theory]
    [InlineData("PostgreSql", UserInfoPublicationFailure.Callback, Traits = ["Provider", "PostgreSql", "CriticalDecision", "managed-userinfo-publication"])]
    [InlineData("PostgreSql", UserInfoPublicationFailure.Commit, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", UserInfoPublicationFailure.Cleanup, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", UserInfoPublicationFailure.Expired, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", UserInfoPublicationFailure.Callback, Traits = ["Provider", "SqlServer", "CriticalDecision", "managed-userinfo-publication"])]
    [InlineData("SqlServer", UserInfoPublicationFailure.Commit, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", UserInfoPublicationFailure.Cleanup, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", UserInfoPublicationFailure.Expired, Traits = ["Provider", "SqlServer"])]
    public async Task Given_ConsentedUserInfo_When_ActualLatePublicationBoundaryFails_Then_NoProfileEscapesAndStateRemainsUnchanged(string provider, UserInfoPublicationFailure failure)
    {
        var fault = new ManagedUserInfoFault(failure);
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider, fault.Configure);
        await ManagedConsentTest.SeedAsync(test);
        await ManagedConsentTest.EmailAsync(test, ManagedConsentTest.Address, true);
        var flow = new ManagedTokenFlow(test, Requested);
        await flow.ApproveAsync();
        using var exchange = await flow.ExchangeAsync(await flow.CodeAsync());
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        using var body = JsonDocument.Parse(await exchange.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        var access = body.RootElement.GetProperty(AccessField).GetString()!;
        var row = Assert.Single((await test.SnapshotAsync()).Interactions);
        fault.Advance = () => test.Http.Environment.Issuer.Account.Journey.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(row.ExpiresAt);
        var before = await ManagedUserInfoEvidence.CaptureAsync(test);
        fault.Armed = true;
        using var response = await ManagedConsentTest.ReadAsync(test, access);
        if (failure == UserInfoPublicationFailure.Callback && response.StatusCode == HttpStatusCode.OK)
        {
            await ManagedConsentTest.RequireAsync(response, row.UserId!, ManagedConsentTest.Address, true);
            Assert.Equal(InjectedValue, Assert.Single(response.Headers.GetValues(UnapprovedHeader)));
            Assert.Equal(1, fault.Faults);
            Assert.Equal(1, fault.Disposals);
            Assert.Equal(1, fault.Commits);
            await before.RequireAsync(test, commits: 1);
            Assert.Fail(PublicationBypass);
        }
        await ManagedCacheTestContext.UnavailableAsync(response);
        Assert.False(response.Headers.Contains(UnapprovedHeader));
        Assert.Equal(1, fault.Faults);
        Assert.Equal(1, fault.Disposals);
        Assert.Equal(failure == UserInfoPublicationFailure.Callback ? 0 : 1, fault.Commits);
        await before.RequireAsync(test, commits: failure == UserInfoPublicationFailure.Callback ? 0 : 1);
    }
}
