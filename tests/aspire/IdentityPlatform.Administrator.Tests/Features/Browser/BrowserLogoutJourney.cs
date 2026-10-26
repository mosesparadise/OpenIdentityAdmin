using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Exercises only genuine browser forms and privately carries the product-generated logout destinations.</summary>
internal static class BrowserLogoutJourney
{
    internal const string IssuerCookie = "__Host-identity-administrator";
    internal const string IssuerPath = "/connect/end-session";
    internal const string Confirm = "Sign out this issuer session";
    internal const string Unverified = "Issuer sign-out result is unverified.";
    internal const string Unknown = "Local sign-out could not be confirmed.";
    private const string ConfirmReport = "b3-issuer-confirm redirects-before={0} redirects-after={1}";
    private const string Link = "#issuer-sign-out";
    private const string Href = "href";
    private const string State = "state";
    private const string Main = "main";
    private const string LocalChoice = "button[name=Mode][value=local]";
    private const string ReferrerHeader = "referrer-policy";
    private const string NoReferrer = "no-referrer";
    private const string LocationHeader = "location";
    private const string CookieHeader = "set-cookie";

    /// <summary>Starts with real password/MFA and proves the local commit before allowing any issuer navigation.</summary>
    internal static async Task<string> BeginAsync(BrowserHttpsFixture fixture, IPage page)
    {
        await BrowserHttpsJourney.SignInAsync(fixture, page);
        await BrowserHttpsJourney.CookiesAsync(fixture, page);
        var source = await BrowserHttpsEffects.SourceAsync(fixture);
        await BrowserHttpsJourney.LogoutAsync(fixture, page, issuer: true);
        await BrowserHttpsJourney.RequireCookiesAsync(fixture, page, issuer: true, remote: true);
        await BrowserHttpsEffects.LocalAsync(fixture, source, BrowserLogoutState.RemotePending);
        await BrowserHttpsEffects.IssuerAsync(fixture, source, revoked: false);
        return source;
    }

    /// <summary>Accepts only the actual fixed issuer link; sensitive query values never become assertion messages.</summary>
    internal static async Task<Uri> LinkAsync(BrowserHttpsFixture fixture, IPage page)
    {
        var link = await page.Locator(Link).GetAttributeAsync(Href);
        Assert.True(Uri.TryCreate(link, UriKind.Absolute, out var uri) &&
            uri.GetLeftPart(UriPartial.Path) == new Uri(fixture.Profile.Issuer, IssuerPath).AbsoluteUri);
        return new Uri(link!);
    }

    /// <summary>Derives only the product's original state-only return; possession is not treated as issuer authority.</summary>
    internal static Uri Return(BrowserHttpsFixture fixture, Uri link) => new(QueryHelpers.AddQueryString(
        new Uri(fixture.Profile.ClientUri, BrowserHttpsJourney.Return).AbsoluteUri, State, QueryHelpers.ParseQuery(link.Query)[State].ToString()));

    /// <summary>Requires actual issuer confirmation and its native redirect before inspecting the unverified BFF result.</summary>
    internal static async Task ConfirmAsync(BrowserHttpsFixture fixture, IPage page)
    {
        var before = fixture.EndSessionRedirects;
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = Confirm,
            Exact = true
        }).ClickAsync();
        await RequireReturnedAsync(fixture, page);
        var after = fixture.EndSessionRedirects;
        TestContext.Current.TestOutputHelper?.WriteLine(ConfirmReport, before, after);
        Assert.Equal(before + 1, after);
    }

    /// <summary>Requires the finite visible outcome and fixed destination without printing selectors from the URL.</summary>
    internal static async Task RequireReturnedAsync(BrowserHttpsFixture fixture, IPage page)
    {
        await page.Locator(Main).Filter(new()
        {
            HasText = Unverified
        }).WaitForAsync();
        Assert.True(Uri.TryCreate(page.Url, UriKind.Absolute, out var uri) &&
            uri.GetLeftPart(UriPartial.Path) == new Uri(fixture.Profile.ClientUri, BrowserHttpsJourney.Return).AbsoluteUri);
    }

    /// <summary>Observes the real browser response status without exposing its response body or destination.</summary>
    internal static async Task DeniedAsync(IPage page, Uri destination)
    {
        var response = await page.GotoAsync(destination.AbsoluteUri);
        Assert.Equal(StatusCodes.Status400BadRequest, response!.Status);
    }

    /// <summary>Retains the existing issuer boundary's finite denial outcomes, with no redirect or cookie publication; durable effects remain independently required.</summary>
    internal static async Task RequireIssuerDeniedAsync(IResponse response)
    {
        Assert.True(response.Status is StatusCodes.Status400BadRequest or StatusCodes.Status503ServiceUnavailable);
        var headers = await response.AllHeadersAsync();
        Assert.False(headers.ContainsKey(CookieHeader) || headers.ContainsKey(LocationHeader));
        Assert.True(headers.TryGetValue(ReferrerHeader, out var policy) && policy == NoReferrer);
    }

    /// <summary>Checks an actual issuer navigation against its existing safe-denial contract without broadening BFF return assertions.</summary>
    internal static async Task IssuerDeniedAsync(IPage page, Uri destination)
    {
        var response = await page.GotoAsync(destination.AbsoluteUri);
        Assert.NotNull(response);
        await RequireIssuerDeniedAsync(response);
    }

    /// <summary>Submits the real loaded local form and requires finite uncertainty without any cookie mutation or issuer navigation.</summary>
    internal static async Task UnconfirmedAsync(BrowserHttpsFixture fixture, IPage page)
    {
        await page.GotoAsync(new Uri(fixture.Profile.ClientUri, BrowserHttpsJourney.Logout).AbsoluteUri);
        var response = await page.RunAndWaitForResponseAsync(() => page.Locator(LocalChoice).ClickAsync(),
            response => response.Request.Method == HttpMethods.Post && Uri.TryCreate(response.Url, UriKind.Absolute, out var uri) &&
                uri.GetLeftPart(UriPartial.Path) == new Uri(fixture.Profile.ClientUri, BrowserHttpsJourney.Logout).AbsoluteUri);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.Status);
        Assert.False((await response.AllHeadersAsync()).ContainsKey(CookieHeader));
        await page.Locator(Main).Filter(new()
        {
            HasText = Unknown
        }).WaitForAsync();
        Assert.Equal(0, await page.Locator(Link).CountAsync());
        Assert.Equal(0, fixture.EndSessionGets);
        Assert.Equal(0, fixture.EndSessionPosts);
    }
}
