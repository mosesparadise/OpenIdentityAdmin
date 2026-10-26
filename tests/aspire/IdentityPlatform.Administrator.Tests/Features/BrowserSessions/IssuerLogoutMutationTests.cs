using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires the native logout hint to select the same genuine source as the authenticated issuer cookie before creating confirmation.</summary>
[Collection("administrator-bootstrap")]
public sealed class IssuerLogoutMutationTests(BootstrapFixture fixture)
{
    private const string Marker = "browser-issuer-source-bypass";
    private const string CookieSeparator = "; ";
    private const string CookieDelimiter = ",";

    /// <summary>An actual other source may not acquire and submit confirmation for the requested source; the marker requires proven wrong-source revocation.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    [Trait("CriticalDecision", "browser-issuer-source")]
    public async Task Given_TwoGenuineIssuerSources_When_CookieAndHintDisagreeBeforeConfirmation_Then_NeitherWrongSourceRevokes(string provider)
    {
        await using var scenario = await IssuerLogoutFixture.CreateAsync(fixture, provider);
        var other = scenario.Login.Issuer.Account.Grant.SessionId;
        Assert.True(other != scenario.Source);
        var cookies = new CookieContainer();
        cookies.SetCookies(scenario.Host.Issuer, scenario.Login.Issuer.Cookie.Replace(CookieSeparator, CookieDelimiter, StringComparison.Ordinal));
        using var browser = scenario.Host.Browser(cookies);
        var own = new Dictionary<string, string>(scenario.Fields) { [IssuerLogoutFixture.Hint] = other };
        using var control = await scenario.GetAsync(own, browser);
        Assert.Equal(HttpStatusCode.OK, control.StatusCode);
        using var requested = await scenario.GetAsync(browser: browser);
        var bypass = requested.StatusCode == HttpStatusCode.OK;
        if (bypass)
        {
            var html = await requested.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            var form = scenario.Fields.Keys.Append(IssuerLogoutFixture.Confirmation).Append(AdministratorJourney.Antiforgery)
                .ToDictionary(name => name, name => AdministratorJourney.Input(html, name), StringComparer.Ordinal);
            using var committed = await scenario.PostAsync(form, browser);
            await scenario.RequireReturnAsync(committed);
            var effects = await scenario.EffectsAsync();
            Assert.True(effects.Sessions[other] && !effects.Sessions[scenario.Source]);
            Assert.True(effects.Sessions.Where(pair => pair.Key != other).All(pair => !pair.Value));
            Assert.Equal(1, effects.Audits);
        }
        else
        {
            IssuerLogoutTests.RequireDenied(requested);
            var before = await scenario.EffectsAsync();
            Assert.True(before.Sessions.Values.All(revoked => !revoked));
            Assert.Equal(0, before.Audits);
            using var committed = await scenario.PostAsync(await scenario.FormAsync());
            await scenario.RequireReturnAsync(committed);
            await scenario.RequireRevokedAsync();
        }
        Assert.False(bypass, Marker);
    }
}
