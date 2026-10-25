using System.Net;
using System.Text.RegularExpressions;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Uses two genuinely authenticated browsers sharing real storage and keys to prove independent correlation pairing.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserRemoteLogoutPairingTests(BootstrapFixture fixture)
{
    private const string IssuerLinkPattern = "id=\"issuer-sign-out\" href=\"([^\"]+)\"";
    private const string ReturnParameter = "post_logout_redirect_uri";
    private const string StateParameter = "state";
    /// <summary>Each authentic logout proof works only with its own browser-generation proof, even when both envelopes share keys and remain current.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_TwoAuthenticBrowserLogoutPairs_When_TheirCookiesAreCrossPaired_Then_NeitherCorrelationIsConsumedAndOriginalPairsWork(string provider)
    {
        await using var scenario = await BrowserRemoteLogoutFixture.CreateAsync(fixture, provider);
        await scenario.BeginAsync();
        var first = scenario.Login.Cookies.GetCookieHeader(scenario.Login.Profile.ClientUri);
        var firstProof = scenario.Login.Cookies.GetCookies(scenario.Login.Profile.ClientUri)[BrowserSessionCookies.Logout]!.Value;
        var jar = new CookieContainer();
        using var browser = scenario.Login.Host.Browser(jar);
        await SignInAsync(scenario.Login, browser);
        using var posted = await scenario.PostAsync(browser);
        await BrowserRemoteLogoutFixture.RequireLocalAsync(posted);
        var secondReturn = await ReturnAsync(posted);
        var second = jar.GetCookieHeader(scenario.Login.Profile.ClientUri);
        var secondProof = jar.GetCookies(scenario.Login.Profile.ClientUri)[BrowserSessionCookies.Logout]!.Value;
        var before = await scenario.Login.SnapshotAsync();
        using var swapped = await BrowserCallbackTests.ReplayAsync(scenario.Login.Host, secondReturn, first.Replace(firstProof, secondProof, StringComparison.Ordinal));
        BrowserLoginTests.RequireDenied(swapped, HttpStatusCode.BadRequest);
        using var reverse = await BrowserCallbackTests.ReplayAsync(scenario.Login.Host, scenario.Return, second.Replace(secondProof, firstProof, StringComparison.Ordinal));
        BrowserLoginTests.RequireDenied(reverse, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.Login.SnapshotAsync());
        using var original = await BrowserCallbackTests.ReplayAsync(scenario.Login.Host, scenario.Return, first);
        await BrowserRemoteLogoutTests.RequireReturnedAsync(original);
        using var other = await browser.GetAsync(secondReturn, TestContext.Current.CancellationToken);
        await BrowserRemoteLogoutTests.RequireReturnedAsync(other);
    }

    /// <summary>Uses the second browser's actual product start, fresh issuer password/MFA and native callback on the same host.</summary>
    private static async Task SignInAsync(BrowserLoginFixture login, HttpClient browser)
    {
        var intent = await login.StartAsync(await login.EntryAsync(browser), browser);
        var completion = await login.CompleteAsync(intent);
        using var resumed = await login.ResumeAsync(completion, browser);
        Assert.Equal(HttpStatusCode.Redirect, resumed.StatusCode);
        var callback = await login.AuthorizeAsync(resumed.Headers.Location!, login.IssuerCookies.GetCookieHeader(login.Profile.Issuer));
        using var accepted = await browser.GetAsync(callback, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, accepted.StatusCode);
    }

    /// <summary>Extracts only the product-produced fixed link in memory and constructs its state-only return for an intentionally unverified observation.</summary>
    private static async Task<Uri> ReturnAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var match = Regex.Match(html, IssuerLinkPattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        Assert.True(match.Success);
        var link = new Uri(WebUtility.HtmlDecode(match.Groups[1].Value));
        var query = QueryHelpers.ParseQuery(link.Query);
        return new(QueryHelpers.AddQueryString(query[ReturnParameter][0]!, StateParameter, query[StateParameter][0]!));
    }
}
