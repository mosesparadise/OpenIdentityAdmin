using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Checks genuine callback response nondelivery and independently reloaded protected material without claiming network loss or process restart.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLogoutLifecycleTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private const string CookieSeparator = "; ";
    private const string CookieDelimiter = ",";
    private static readonly bool[] Replacements = [false, true];

    /// <summary>A genuine callback cookie delivered after explicit logout or replacement cannot revive the orphaned older generation.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_DiscardedCallbackDelivery_When_OriginalBrowserCancelsOrReplaces_Then_LateGenuineCookiesCannotRestoreAuthentication(string provider, bool replace)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var callback = await scenario.CallbackAsync();
        var original = scenario.Cookies.GetCookieHeader(callback);
        var late = new CookieContainer();
        late.SetCookies(scenario.Profile.ClientUri, original.Replace(CookieSeparator, CookieDelimiter, StringComparison.Ordinal));
        using var detached = scenario.Host.Browser(late);
        using var callbackResponse = await detached.GetAsync(callback, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, callbackResponse.StatusCode);
        var issued = late.GetCookieHeader(scenario.Profile.ClientUri);
        using var status = await detached.GetAsync(BrowserLogoutHttp.Status, TestContext.Current.CancellationToken);
        Assert.Contains("\"authenticated\":true", await status.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        if (replace)
        {
            _ = await scenario.StartAsync(await scenario.EntryAsync());
        }
        else
        {
            using var logout = await BrowserLogoutHttp.PostAsync(scenario.Browser, await BrowserLogoutHttp.EntryAsync(scenario.Browser));
            await BrowserLogoutHttp.CommittedAsync(logout);
        }
        Assert.NotNull(await scenario.ReadAsync(db => db.Set<AdminSessionTicket>().Select(row => row.RevokedAt).SingleAsync(TestContext.Current.CancellationToken)));
        var before = await scenario.SnapshotAsync();
        await BrowserLogoutHttp.DeniedStatusAsync(scenario.Host, scenario.Profile.ClientUri, issued);
        using var replay = await BrowserCallbackTests.ReplayAsync(scenario.Host, callback, original);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(1, scenario.Host.Bridge.TokenSends);
        Assert.Equal(1, scenario.Host.Bridge.ParSends);
    }

    /// <summary>Lost protection keys deny without changing state; reloaded retained files admit the genuine session and persist logout across another fresh root.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_GenuineSessionAcrossReloadedRoots_When_ProtectionIsMissingThenRetained_Then_OnlyRetainedKeysPermitProtectedLogout(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        using var login = await scenario.Browser.GetAsync(await scenario.CallbackAsync(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, login.StatusCode);
        var original = scenario.Cookies.GetCookieHeader(scenario.Profile.ClientUri);
        using var material = new BffSessionFixture();
        using var missing = material.Protection();
        using var imported = scenario.ReloadCredentials();
        await using var incapable = await scenario.ReloadAsync(missing, imported);
        var before = await scenario.SnapshotAsync();
        await BrowserLogoutHttp.DeniedStatusAsync(incapable, scenario.Profile.ClientUri, original);
        using var denied = await BrowserCallbackTests.ReplayAsync(incapable, new Uri(scenario.Profile.ClientUri, BrowserLogoutHttp.Path), original);
        BrowserLoginTests.RequireDenied(denied, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
        using (var retained = scenario.ReloadProtection())
        {
            await using var capable = await scenario.ReloadAsync(retained, imported);
            using var browser = capable.Browser(scenario.Cookies);
            using var status = await browser.GetAsync(BrowserLogoutHttp.Status, TestContext.Current.CancellationToken);
            Assert.Contains("\"authenticated\":true", await status.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
            using var logout = await BrowserLogoutHttp.PostAsync(browser, await BrowserLogoutHttp.EntryAsync(browser));
            await BrowserLogoutHttp.CommittedAsync(logout);
        }
        using var reloaded = scenario.ReloadProtection();
        await using var observer = await scenario.ReloadAsync(reloaded, imported);
        await BrowserLogoutHttp.DeniedStatusAsync(observer, scenario.Profile.ClientUri, original);
        await BrowserLogoutTests.RequireCommittedAsync(scenario, authenticated: true);
        Assert.Equal(0, incapable.Bridge.TokenSends);
        Assert.Equal(0, observer.Bridge.TokenSends);
    }

    /// <summary>Enumerates both explicit orphan-recovery transitions on each real database.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Cases => from provider in IssuerContinuationProviderFixture.Providers
                                                                    from replace in Replacements
                                                                    select new TheoryDataRow<string, bool>(provider, replace).WithTrait(ProviderTrait, provider);
}
