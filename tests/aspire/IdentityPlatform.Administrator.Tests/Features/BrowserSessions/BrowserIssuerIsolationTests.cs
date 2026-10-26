using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Checks reverse route substitution and explicit OTP retry against actual native password and factor verification.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserIssuerIsolationTests(BootstrapFixture fixture)
{
    /// <summary>A standalone password interaction cannot become browser completion; its real unused OTP remains usable at the original standalone page.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_StandaloneInteraction_When_SubmittedToBrowserMfa_Then_OriginalStandaloneAuthorityRemainsUnspent(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        _ = await scenario.EnrollAsync();
        await using var host = await BrowserIssuerHost.CreateAsync(scenario, provider);
        using var browser = host.Browser();
        var entry = await BrowserIssuerHttp.EntryAsync(browser, IssuerContinuationProviderFixture.Selector());
        using var page = await browser.GetAsync(AdministratorJourney.Login, TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var password = await BrowserIssuerHttp.PostAsync(browser, AdministratorJourney.Login, new Dictionary<string, string>
        {
            [AdministratorJourney.Antiforgery] = AdministratorJourney.Input(html, AdministratorJourney.Antiforgery),
            [BrowserIssuerHttp.Username] = scenario.Lease.Username,
            [BrowserIssuerHttp.Password] = scenario.Lease.Password
        });
        Assert.Equal(HttpStatusCode.Redirect, password.StatusCode);
        var factor = await AdministratorJourney.ReadAsync(browser, password.Headers.Location!, enrollment: false);
        var code = await IssuerContinuationProviderFixture.UnusedCodeAsync(scenario);
        var form = new Dictionary<string, string>(entry, StringComparer.Ordinal)
        {
            [AdministratorJourney.Interaction] = factor.Interaction,
            [AdministratorJourney.Antiforgery] = factor.Antiforgery,
            [AdministratorJourney.CodeField] = code
        };
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        using var denied = await BrowserIssuerHttp.PostAsync(browser, BrowserIssuerHttp.Mfa, form);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Null(denied.Headers.Location);
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
        using var accepted = await AdministratorJourney.ConfirmAsync(browser, factor, code, enrollment: false);
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.Equal(AdministratorJourney.Security, accepted.Headers.Location?.OriginalString);
    }

    /// <summary>A wrong OTP never authenticates or resets the original browser intent; the subsequent actual unused OTP completes that same admission.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_AdmittedBrowserPassword_When_OtpIsWrongThenCorrect_Then_OriginalDeadlineAndIntentArePreserved(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        _ = await scenario.EnrollAsync();
        await using var host = await BrowserIssuerHost.CreateAsync(scenario, provider);
        var cookies = new CookieContainer();
        using var browser = host.Browser(cookies);
        var intent = IssuerContinuationProviderFixture.Selector();
        var entry = await BrowserIssuerHttp.EntryAsync(browser, intent);
        using var password = await BrowserIssuerHttp.PasswordAsync(browser, scenario, entry);
        Assert.Null(cookies.GetCookies(host.Issuer)[AdministratorJourney.SessionCookie]);
        var form = await BrowserIssuerHttp.FactorAsync(browser, password);
        var original = await BrowserIssuerJourneyTests.ContinuationAsync(scenario, intent);
        form[AdministratorJourney.CodeField] = FactorOracle.InvalidCode(scenario.Seed);
        using var denied = await BrowserIssuerHttp.PostAsync(browser, BrowserIssuerHttp.Mfa, form);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        await BrowserFormHeaders.AccountAsync(denied, mfa: true);
        Assert.Null(cookies.GetCookies(host.Issuer)[AdministratorJourney.SessionCookie]);
        var after = await BrowserIssuerJourneyTests.ContinuationAsync(scenario, intent);
        Assert.Equal(original.ExpiresAt, after.ExpiresAt);
        Assert.Equal(original.CorrelationId, after.CorrelationId);
        form[AdministratorJourney.CodeField] = await IssuerContinuationProviderFixture.UnusedCodeAsync(scenario);
        using var accepted = await BrowserIssuerHttp.PostAsync(browser, BrowserIssuerHttp.Mfa, form);
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        BrowserFormHeaders.Referrer(accepted);
        Assert.NotNull(cookies.GetCookies(host.Issuer)[AdministratorJourney.SessionCookie]);
    }
}
