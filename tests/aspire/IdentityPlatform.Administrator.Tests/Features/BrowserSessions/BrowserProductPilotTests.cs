using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Presents genuine same-issuer pilot authority to the actual B3 authorization and product callback.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserProductPilotTests(BootstrapFixture fixture)
{
    /// <summary>A real password-only pilot cookie cannot satisfy the fresh bound administrator source session or establish B1 authority.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_GenuineSameIssuerPilotCookie_When_SubstitutedAtProductAuthorization_Then_NoAdministratorTicketIsCreated(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var pilot = await BrowserPilotCookie.CreateAsync(scenario, provider);
        var callback = await scenario.AuthorizeAsync(await scenario.ChallengeAsync(), pilot, error: true);
        var cookies = scenario.Cookies.GetCookieHeader(callback);
        using var denied = await scenario.Browser.GetAsync(callback, TestContext.Current.CancellationToken);
        BrowserLoginTests.RequireDenied(denied, HttpStatusCode.ServiceUnavailable);
        await BrowserCallbackTests.RequireNoTicketAsync(scenario, sends: 0);
        var before = await scenario.SnapshotAsync();
        using var replay = await BrowserCallbackTests.ReplayAsync(scenario.Host, callback, cookies);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
    }
}
