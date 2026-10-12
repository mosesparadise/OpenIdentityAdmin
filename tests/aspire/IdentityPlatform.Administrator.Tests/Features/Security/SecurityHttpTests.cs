using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.Security;

/// <summary>Exercises fixed voluntary security routes with real named cookies, antiforgery and cross-node persisted authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class SecurityHttpTests(BootstrapFixture fixture)
{
    private const string ReusedRecoveryMaterial = "New recovery material reused a previously disclosed code.";
    /// <summary>The current cookie and actual fresh password/TOTP are required before a new authenticator is staged and installed.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CurrentBrowser_When_VoluntaryReplacementCompletes_Then_NewFactorAndSessionWorkOnOtherNode(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var cookies = new CookieContainer();
        using var browser = environment.Browser(cookies);
        var enrolled = await SecurityHttp.EnrollAsync(environment, browser);
        var initial = await AdministratorJourney.BeginAtAsync(environment, browser, SecurityHttp.Replacement);
        var form = await AdministratorJourney.ReadAsync(browser, initial, enrollment: false);
        var replacement = await SecurityHttp.TransitionAsync(browser, form, FactorOracle.UnusedCode(enrolled.Seed, enrolled.AcceptedCode), recovery: false);
        using var other = environment.Browser(cookies, secondary: true);
        var staged = await AdministratorJourney.ReadAsync(other, replacement, enrollment: true);
        using var completed = await AdministratorJourney.ConfirmAtAsync(other, staged,
            FactorOracle.Code(staged.Seed!, DateTimeOffset.UtcNow.ToUnixTimeSeconds()), SecurityHttp.Confirm);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(10, (await SecurityHttp.CodesAsync(completed)).Length);
        using var status = await browser.GetAsync(AdministratorJourney.Security, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
    }

    /// <summary>Exact-purpose regeneration rotates the current cookie and exposes its new code collection only once.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CurrentBrowser_When_CodesAreRegenerated_Then_ReplacementCookieAndOneTimeResponseCommit(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var cookies = new CookieContainer();
        using var browser = environment.Browser(cookies);
        var enrolled = await SecurityHttp.EnrollAsync(environment, browser);
        var cookie = cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie]!.Value;
        var initial = await AdministratorJourney.BeginAtAsync(environment, browser, SecurityHttp.Regeneration);
        var form = await AdministratorJourney.ReadAsync(browser, initial, enrollment: false);
        var code = FactorOracle.UnusedCode(enrolled.Seed, enrolled.AcceptedCode);
        using var wrongPurpose = await AdministratorJourney.ConfirmAtAsync(browser, form, code, SecurityHttp.Replace);
        Assert.Equal(HttpStatusCode.BadRequest, wrongPurpose.StatusCode);
        using var completed = await AdministratorJourney.ConfirmAtAsync(browser, form, code, SecurityHttp.Regenerate);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var codes = await SecurityHttp.CodesAsync(completed);
        Assert.False(codes.Intersect(enrolled.RecoveryCodes, StringComparer.Ordinal).Any(), ReusedRecoveryMaterial);
        Assert.True(cookie != cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie]!.Value);
        using var repeated = await AdministratorJourney.ConfirmAtAsync(browser, form, code, SecurityHttp.Regenerate);
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
    }

    /// <summary>Logout GET and missing antiforgery retain authority; the legitimate POST revokes and clears exactly the current cookie.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CurrentBrowser_When_LogoutGetThenProtectedPostRuns_Then_OnlyPostEndsStoredSession(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var cookies = new CookieContainer();
        using var browser = environment.Browser(cookies);
        await SecurityHttp.EnrollAsync(environment, browser);
        using var confirmation = await browser.GetAsync(SecurityHttp.Logout, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        var csrf = AdministratorJourney.Input(await confirmation.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), AdministratorJourney.Antiforgery);
        using var noCsrf = await AdministratorJourney.PostAsync(browser, SecurityHttp.Logout, []);
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        using var alive = await browser.GetAsync(AdministratorJourney.Security, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
        using var completed = await AdministratorJourney.PostAsync(browser, SecurityHttp.Logout, new Dictionary<string, string>
        {
            [AdministratorJourney.Antiforgery] = csrf
        });
        Assert.Equal(HttpStatusCode.Redirect, completed.StatusCode);
        Assert.True(cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie] is null);
        using var other = environment.Browser(cookies, secondary: true);
        using var denied = await other.GetAsync(AdministratorJourney.Security, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }
}
