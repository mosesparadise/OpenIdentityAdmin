using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Preserves native error consumption without treating an unsuccessful issuer authorization as a reusable login attempt.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserCallbackErrorTests(BootstrapFixture fixture)
{
    /// <summary>The genuine issuer rejects an older same-account cookie; its native error callback consumes state without exchanging or establishing.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_OlderIssuerCookie_When_ActualAuthorizationErrorReturns_Then_ProductCallbackSpendsStateWithoutExchangeOrSession(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var callback = await scenario.CallbackAsync(fresh: false);
        var cookies = scenario.Cookies.GetCookieHeader(callback);
        using var response = await scenario.Browser.GetAsync(callback, TestContext.Current.CancellationToken);
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
        await BrowserCallbackTests.RequireNoTicketAsync(scenario, sends: 0);
        var before = await scenario.SnapshotAsync();
        using var replay = await BrowserCallbackTests.ReplayAsync(scenario.Host, callback, cookies);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
    }
}
