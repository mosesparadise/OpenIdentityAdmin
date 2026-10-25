using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Verifies native confirmation and exact-source logout with genuine browser authority on both relational providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class IssuerLogoutTests(BootstrapFixture fixture)
{
    private const string CookieSeparator = "; ";
    private const string CookieDelimiter = ",";
    private const string ForeignOrigin = "https://foreign.example.test";
    private const string InvalidToken = "invalid";
    private const string SetCookie = "Set-Cookie";

    /// <summary>GET is inert; explicit native-antiforgery confirmation revokes exactly the fresh source while replay grants no second outcome.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_GenuineBrowserSession_When_NativeLogoutIsConfirmed_Then_OnlyItsSourceAndOneAuditCommit(string provider)
    {
        await using var scenario = await IssuerLogoutFixture.CreateAsync(fixture, provider);
        var before = await scenario.EffectsAsync();
        var form = await scenario.FormAsync();
        var inert = await scenario.EffectsAsync();
        Assert.True(before.Sessions.Count == inert.Sessions.Count && before.Sessions.All(pair => inert.Sessions.TryGetValue(pair.Key, out var value) && value == pair.Value));
        Assert.Equal(before.Audits, inert.Audits);
        using var response = await scenario.PostAsync(form);
        await scenario.RequireReturnAsync(response);
        await scenario.RequireRevokedAsync();
        using var replay = await scenario.PostAsync(form);
        RequireDenied(replay);
        await scenario.RequireRevokedAsync();
        Assert.Equal(1, scenario.Login.Host.Bridge.TokenSends);
        Assert.Equal(1, scenario.Login.Host.Bridge.ParSends);
    }

    /// <summary>Another genuine source cookie cannot confirm the old session, even when it belongs to the same administrator.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ConfirmationForFreshSource_When_AnotherActualIssuerSessionSubmits_Then_NeitherSourceIsRevoked(string provider)
    {
        await using var scenario = await IssuerLogoutFixture.CreateAsync(fixture, provider);
        var form = await scenario.FormAsync();
        var cookies = new CookieContainer();
        cookies.SetCookies(scenario.Host.Issuer, scenario.Login.Issuer.Cookie.Replace(CookieSeparator, CookieDelimiter, StringComparison.Ordinal));
        using var browser = scenario.Host.Browser(cookies);
        var own = new Dictionary<string, string>(scenario.Fields) { [IssuerLogoutFixture.Hint] = scenario.Login.Issuer.Account.Grant.SessionId };
        using var ownConfirmation = await scenario.GetAsync(own, browser);
        Assert.Equal(HttpStatusCode.OK, ownConfirmation.StatusCode);
        using var denied = await scenario.PostAsync(form, browser);
        RequireDenied(denied);
        var effects = await scenario.EffectsAsync();
        Assert.True(effects.Sessions.Values.All(revoked => !revoked));
        Assert.Equal(0, effects.Audits);
        using var control = await scenario.PostAsync(form);
        await scenario.RequireReturnAsync(control);
        await scenario.RequireRevokedAsync();
    }

    /// <summary>Cross-origin, changed native state and absent native antiforgery do not consume the original genuine confirmation.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ValidConfirmation_When_RequestOrAntiforgeryChanges_Then_DenialPreservesThePositiveControl(string provider)
    {
        await using var scenario = await IssuerLogoutFixture.CreateAsync(fixture, provider);
        var form = await scenario.FormAsync();
        using var crossOrigin = await scenario.PostAsync(form, origin: ForeignOrigin);
        RequireDenied(crossOrigin);
        var changed = new Dictionary<string, string>(form) { [IssuerLogoutFixture.State] = IdentityPlatform.Infrastructure.Features.Identity.Ceremonies.CeremonySecrets.Create() };
        using var state = await scenario.PostAsync(changed);
        RequireDenied(state);
        changed = new(form)
        {
            [AdministratorJourney.Antiforgery] = InvalidToken
        };
        using var csrf = await scenario.PostAsync(changed);
        RequireDenied(csrf);
        var effects = await scenario.EffectsAsync();
        Assert.True(effects.Sessions.Values.All(revoked => !revoked));
        Assert.Equal(0, effects.Audits);
        using var control = await scenario.PostAsync(form);
        await scenario.RequireReturnAsync(control);
        await scenario.RequireRevokedAsync();
    }

    /// <summary>Committed but unpublished effects report finite unavailability without a misleading rollback page or success material.</summary>
    internal static async Task RequireUnavailableAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        RequireDenied(response);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Finite denial carries neither a return location nor success cookie changes.</summary>
    internal static void RequireDenied(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.ServiceUnavailable);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains(SetCookie));
    }
}
