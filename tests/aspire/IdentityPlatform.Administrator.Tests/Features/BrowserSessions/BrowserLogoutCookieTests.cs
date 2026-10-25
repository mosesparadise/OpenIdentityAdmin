using System.Net;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Rejects a live foreign B1 cookie during ordinary logout and stale-proof reconciliation using real product sessions.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLogoutCookieTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private const string Separator = "; ";
    private const string Assignment = "=";
    private const string CookieRoot = "/";
    private const string InvalidHandle = "invalid";
    private const string Foreign = "foreign";
    private const string Duplicate = "duplicate";
    private const string Malformed = "malformed";
    private static readonly string[] Faults = [Foreign, Duplicate, Malformed];

    /// <summary>Current positive proof admission never treats malformed/duplicate or a live foreign session as anonymous cancellation authority.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_GenuinePendingBrowser_When_SessionCookieIsAmbiguousOrForeign_Then_LogoutFormDeniesWithoutMutation(string provider, string fault)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        _ = await scenario.StartAsync(await scenario.EntryAsync());
        var original = scenario.Cookies.GetCookieHeader(scenario.Profile.ClientUri);
        var handle = fault == Foreign ? await ForeignHandleAsync(scenario) : InvalidHandle;
        var presented = original + Separator + BrowserSessionCookies.Session + Assignment + handle;
        if (fault == Duplicate)
        {
            presented += Separator + BrowserSessionCookies.Session + Assignment + handle;
        }
        var before = await scenario.SnapshotAsync();
        using var denied = await BrowserCallbackTests.ReplayAsync(scenario.Host, new Uri(scenario.Profile.ClientUri, BrowserLogoutHttp.Path), presented);
        BrowserLoginTests.RequireDenied(denied, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
        using var positive = await BrowserLogoutHttp.PostAsync(scenario.Browser, await BrowserLogoutHttp.EntryAsync(scenario.Browser));
        await BrowserLogoutHttp.CommittedAsync(positive);
    }

    /// <summary>Even an otherwise valid recovery proof cannot carry another browser's still-live canonical B1 handle into reconciliation.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ValidLogoutRecoveryProof_When_ForeignLiveTicketIsPresented_Then_NoRecoveryCookiesOrDurableEffectsArePublished(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        using var delivered = await BrowserLogoutReconciliationTests.LostAsync(scenario);
        var handle = await ForeignHandleAsync(scenario);
        scenario.Cookies.Add(scenario.Profile.ClientUri, new Cookie(BrowserSessionCookies.Session, handle, CookieRoot) { Secure = true });
        var before = await scenario.SnapshotAsync();
        using var denied = await scenario.Browser.GetAsync(BrowserLogoutHttp.Path, TestContext.Current.CancellationToken);
        BrowserLoginTests.RequireDenied(denied, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Completes the actual fresh password/MFA/native callback in a second browser sharing the same Sessions store.</summary>
    private static async Task<string> ForeignHandleAsync(BrowserLoginFixture scenario)
    {
        var cookies = new CookieContainer();
        using var browser = scenario.Host.Browser(cookies);
        var intent = await scenario.StartAsync(await scenario.EntryAsync(browser), browser);
        var completion = await scenario.CompleteAsync(intent);
        using var resume = await scenario.ResumeAsync(completion, browser);
        Assert.Equal(HttpStatusCode.Redirect, resume.StatusCode);
        var callback = await scenario.AuthorizeAsync(resume.Headers.Location!, scenario.IssuerCookies.GetCookieHeader(scenario.Profile.Issuer));
        using var accepted = await browser.GetAsync(callback, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, accepted.StatusCode);
        return cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Session]!.Value;
    }

    /// <summary>Discovers independent ambiguity and genuine foreign-authority controls on both stores.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Cases => from provider in IssuerContinuationProviderFixture.Providers
                                                                      from fault in Faults
                                                                      select new TheoryDataRow<string, string>(provider, fault).WithTrait(ProviderTrait, provider);
}
