using System.Net;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires actual status reads to reject stale, cross-browser and duplicate proofs after genuine product login.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserSessionBindingTests(BootstrapFixture fixture)
{
    private const string SetCookie = "Set-Cookie";
    private const string StatusPath = "/bff/session";
    private const string Separator = "; ";
    private const string Assignment = "=";
    private const string Stale = "stale";
    private const string CrossBrowser = "cross-browser";
    private static readonly string[] Faults = [Stale, CrossBrowser, "duplicate"];
    private const string ProviderTrait = "Provider";

    /// <summary>All supplied handles/proofs were originally real; only their pairing, durable generation or unique framing is changed.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_GenuineProductProofs_When_StaleCrossPairedOrDuplicated_Then_StatusDeniesWithoutMutation(string provider, string fault)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        using var login = await scenario.Browser.GetAsync(await scenario.CallbackAsync(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, login.StatusCode);
        var target = new Uri(scenario.Profile.ClientUri, StatusPath);
        var original = scenario.Cookies.GetCookieHeader(target);
        var handle = scenario.Cookies.GetCookies(target)[BrowserSessionCookies.Session]!.Value;
        var presented = original;
        if (fault == Stale)
        {
            using var replacement = await scenario.Browser.GetAsync(await scenario.CallbackAsync(), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.SeeOther, replacement.StatusCode);
        }
        else if (fault == CrossBrowser)
        {
            presented = await CrossPairAsync(scenario, handle);
        }
        else
        {
            presented += Separator + BrowserSessionCookies.Session + Assignment + handle;
        }
        var before = await scenario.SnapshotAsync();
        using var response = await BrowserCallbackTests.ReplayAsync(scenario.Host, target, presented);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"authenticated\":false}", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False(response.Headers.Contains(SetCookie));
        Assert.Null(response.Headers.Location);
        Assert.Equal(before, await scenario.SnapshotAsync());
    }

    /// <summary>Completes a second genuine browser context, then pairs its authentic generation with the first browser's valid handle.</summary>
    private static async Task<string> CrossPairAsync(BrowserLoginFixture scenario, string firstHandle)
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
        var generation = cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Generation]!.Value;
        return BrowserSessionCookies.Session + Assignment + firstHandle + Separator + BrowserSessionCookies.Generation + Assignment + generation;
    }

    /// <summary>Lists every real provider and independent framing/association fault.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Cases => from provider in IssuerContinuationProviderFixture.Providers
                                                                      from fault in Faults
                                                                      select new TheoryDataRow<string, string>(provider, fault).WithTrait(ProviderTrait, provider);
}
