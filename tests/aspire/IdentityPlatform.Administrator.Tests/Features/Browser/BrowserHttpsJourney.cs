using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Uses actual browser navigation and native password/MFA forms; all sensitive values remain in memory.</summary>
internal static class BrowserHttpsJourney
{
    internal const string Login = "/bff/login";
    internal const string Logout = "/bff/logout";
    internal const string Return = "/bff/logout/return";
    private const string IssuerCookie = "__Host-identity-administrator";
    private const int SessionProofCount = 2;
    private const string RootPath = "/";
    private const string CacheControl = "cache-control";
    private const string NoStore = "no-store";
    private const string LocalChoice = "button[name=Mode][value=local]";
    private const string IssuerChoice = "button[name=Mode][value=issuer]";
    private const string Main = "main";
    private const string LocalSuccess = "session has ended locally";
    private const string Status = "/bff/session";
    private const string Start = "Start sign-in";
    private const string Username = "#username";
    private const string Password = "#password";
    private const string Submit = "#submit";
    private const string Factor = "#code";
    private const string Authenticated = "authenticated";
    private const string Subject = "subject";
    private const string Expires = "expiresAt";
    private const string Storage = "JSON.stringify({local: {...localStorage}, session: {...sessionStorage}, cookie: document.cookie})";
    private static readonly string[] StatusFields = [Authenticated, Expires, Subject];

    /// <summary>Requires fresh forms even with an existing issuer session and follows the real native browser callback chain.</summary>
    internal static async Task SignInAsync(BrowserHttpsFixture fixture, IPage page)
    {
        var response = await page.GotoAsync(new Uri(fixture.Profile.ClientUri, Login).AbsoluteUri);
        Assert.Equal(StatusCodes.Status200OK, response!.Status);
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = Start,
            Exact = true
        }).ClickAsync();
        await page.Locator(Username).FillAsync(fixture.Issuer.Account.Journey.Lease.Username);
        await page.Locator(Password).FillAsync(fixture.Issuer.Account.Journey.Lease.Password);
        await page.Locator(Submit).ClickAsync();
        await page.Locator(Factor).FillAsync(await IssuerContinuationProviderFixture.UnusedCodeAsync(fixture.Issuer.Account.Journey));
        await page.Locator(Submit).ClickAsync();
        await page.WaitForURLAsync(url => url == new Uri(fixture.Profile.ClientUri, Login).AbsoluteUri);
        await RequireStatusAsync(fixture, page, true);
    }

    /// <summary>Inspects the real noncacheable browser JSON response without rendering its subject or private transport in assertion messages.</summary>
    internal static async Task RequireStatusAsync(BrowserHttpsFixture fixture, IPage page, bool authenticated)
    {
        var response = await page.GotoAsync(new Uri(fixture.Profile.ClientUri, Status).AbsoluteUri);
        Assert.Equal(StatusCodes.Status200OK, response!.Status);
        Assert.True(response.Headers.TryGetValue(CacheControl, out var cache) && cache.Contains(NoStore, StringComparison.Ordinal));
        using var json = JsonDocument.Parse(await response.TextAsync());
        Assert.Equal(authenticated, json.RootElement.GetProperty(Authenticated).GetBoolean());
        var fields = json.RootElement.EnumerateObject().Select(value => value.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(authenticated ? StatusFields : [Authenticated], fields);
        if (authenticated)
        {
            Assert.True(json.RootElement.GetProperty(Subject).GetString() == fixture.Issuer.Account.ActorId);
        }
    }

    /// <summary>Retains genuine session and generation cookies for independent stale-cookie denial, never synthesizing authority.</summary>
    internal static async Task<Cookie[]> CookiesAsync(BrowserHttpsFixture fixture, IPage page)
    {
        await RequireCookiesAsync(fixture, page, issuer: true, remote: false);
        var cookies = (await page.Context.CookiesAsync()).Where(cookie => cookie.Name is BrowserSessionCookies.Session or BrowserSessionCookies.Generation).ToArray();
        Assert.Equal(SessionProofCount, cookies.Length);
        var storage = await page.EvaluateAsync<string>(Storage);
        Assert.True(cookies.All(cookie => cookie.Domain == fixture.Profile.ClientUri.Host && cookie.Path == RootPath &&
            cookie.Secure && cookie.HttpOnly && cookie.SameSite == SameSiteAttribute.Lax));
        var exposed = cookies.Any(cookie => storage.Contains(cookie.Value, StringComparison.Ordinal));
        Assert.False(exposed);
        Assert.Equal(-1, cookies.Single(cookie => cookie.Name == BrowserSessionCookies.Session).Expires);
        return cookies.Select(cookie => new Cookie
        {
            Name = cookie.Name,
            Value = cookie.Value,
            Domain = cookie.Domain,
            Path = cookie.Path,
            Secure = cookie.Secure,
            HttpOnly = cookie.HttpOnly,
            SameSite = cookie.SameSite,
            Expires = cookie.Expires
        }).ToArray();
    }

    /// <summary>Checks actual browser retention/deletion and protected scope independently of rendered logout text.</summary>
    internal static async Task RequireCookiesAsync(BrowserHttpsFixture fixture, IPage page, bool issuer, bool remote)
    {
        var cookies = await page.Context.CookiesAsync();
        var issuerCount = cookies.Count(cookie => cookie.Name == IssuerCookie);
        var remoteCount = cookies.Count(cookie => cookie.Name == BrowserSessionCookies.Logout);
        Assert.Equal(issuer ? 1 : 0, issuerCount);
        Assert.Equal(remote ? 1 : 0, remoteCount);
        var protectedCookies = cookies.Where(cookie => cookie.Name is IssuerCookie or BrowserSessionCookies.Logout);
        Assert.True(protectedCookies.All(cookie => cookie.Domain == fixture.Profile.ClientUri.Host && cookie.Path == RootPath &&
            cookie.Secure && cookie.HttpOnly && cookie.SameSite == SameSiteAttribute.Lax));
    }

    /// <summary>Loads the inert BFF form and submits the explicit local or issuer choice using browser-generated origin and antiforgery.</summary>
    internal static async Task LogoutAsync(BrowserHttpsFixture fixture, IPage page, bool issuer)
    {
        var response = await page.GotoAsync(new Uri(fixture.Profile.ClientUri, Logout).AbsoluteUri);
        Assert.Equal(StatusCodes.Status200OK, response!.Status);
        await page.Locator(issuer ? IssuerChoice : LocalChoice).ClickAsync();
        Assert.True((await page.TextContentAsync(Main))!.Contains(LocalSuccess, StringComparison.Ordinal));
        var retained = (await page.Context.CookiesAsync()).Any(cookie => cookie.Name == BrowserSessionCookies.Session);
        Assert.False(retained);
    }
}
