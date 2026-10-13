using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.Recovery;

/// <summary>Proves real browser bindings, native antiforgery and cross-node recovery authority through HTTPS Razor endpoints.</summary>
[Collection("administrator-bootstrap")]
public sealed class RecoveryHttpTests(BootstrapFixture fixture)
{
    private const string ReusedRecoveryMaterial = "New recovery material reused a previously disclosed code.";
    private const string RecoveryCookieMarker = "administrator-recovery-cookie-bypass";
    /// <summary>Recovery invalidates the old cookie without issuing another, then only proven replacement creates new local authority.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    [Trait("CriticalDecision", "administrator-recovery-cookie")]
    public async Task Given_EnrolledBrowser_When_RecoveryCompletesAcrossNodes_Then_RestrictedEntryPrecedesNewSessionAndOneTimeCodes(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var cookies = new CookieContainer();
        using var browser = environment.Browser(cookies);
        var enrolled = await SecurityHttp.EnrollAsync(environment, browser);
        var cookie = cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie]!.Value;
        var location = await AdministratorJourney.BeginAtAsync(environment, browser, SecurityHttp.Recovery);
        var challenge = await AdministratorJourney.ReadAsync(browser, location, enrollment: false);
        using var other = environment.Browser(cookies, secondary: true);
        var replacement = await SecurityHttp.TransitionAsync(other, challenge, enrolled.RecoveryCodes[0], recovery: true);
        Assert.True(cookie == cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie]!.Value);
        using var restricted = await other.GetAsync(AdministratorJourney.Security, TestContext.Current.CancellationToken);
        await RequireRestrictedAsync(restricted);
        var confirmation = await AdministratorJourney.ReadAsync(browser, replacement, enrollment: true);
        var code = FactorOracle.Code(confirmation.Seed!, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        using var completed = await AdministratorJourney.ConfirmAtAsync(other, confirmation, code, SecurityHttp.Confirm);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var codes = await SecurityHttp.CodesAsync(completed);
        Assert.False(codes.Intersect(enrolled.RecoveryCodes, StringComparer.Ordinal).Any(), ReusedRecoveryMaterial);
        Assert.True(cookie != cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie]!.Value);
        using var status = await browser.GetAsync(AdministratorJourney.Security, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        using var repeated = await AdministratorJourney.ConfirmAtAsync(browser, confirmation, code, SecurityHttp.Confirm);
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
        using var redisplay = await other.GetAsync(replacement, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, redisplay.StatusCode);
    }

    /// <summary>Marks only demonstrated successful protected-page authority as the mutation bypass; unrelated failures remain unmarked failures.</summary>
    private static async Task RequireRestrictedAsync(HttpResponseMessage response)
    {
        const string AuthorityText = "You are signed in with your password and authenticator.";
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.True(html.Contains(AuthorityText, StringComparison.Ordinal));
            Assert.Fail(RecoveryCookieMarker);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Another browser's valid antiforgery token cannot spend the owner's recovery proof or receive its staged key.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_BoundRecovery_When_ForeignBrowserOrMissingCsrfSubmits_Then_OwnerProofRemainsUsable(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        using var owner = environment.Browser(new CookieContainer());
        var enrolled = await SecurityHttp.EnrollAsync(environment, owner);
        var location = await AdministratorJourney.BeginAtAsync(environment, owner, SecurityHttp.Recovery);
        var form = await AdministratorJourney.ReadAsync(owner, location, enrollment: false);
        using var stranger = environment.Browser(new CookieContainer(), secondary: true);
        var foreign = await AdministratorJourney.ReadAsync(stranger, location, enrollment: false);
        using var denied = await AdministratorJourney.ConfirmAtAsync(stranger, foreign, enrolled.RecoveryCodes[0], SecurityHttp.RecoveryVerify);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        using var noCsrf = await AdministratorJourney.PostAsync(owner, SecurityHttp.RecoveryVerify, new Dictionary<string, string>
        {
            [AdministratorJourney.Interaction] = form.Interaction,
            [AdministratorJourney.CodeField] = enrolled.RecoveryCodes[0]
        });
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        var replacement = await SecurityHttp.TransitionAsync(owner, form, enrolled.RecoveryCodes[0], recovery: true);
        using var foreignSeed = await stranger.GetAsync(replacement, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, foreignSeed.StatusCode);
        Assert.NotNull((await AdministratorJourney.ReadAsync(owner, replacement, enrollment: true)).Seed);
    }
}
