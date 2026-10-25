using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises only the actual fixed logout form and native antiforgery POST over a caller-owned browser.</summary>
internal static class BrowserLogoutHttp
{
    private const string LocalOutcome = "session has ended locally";
    private const string RemoteOutcome = "Issuer sign-out was not requested.";
    private const string ScriptElement = "<script";
    private const string AnonymousStatus = "{\"authenticated\":false}";
    private const string SetCookie = "Set-Cookie";
    internal const string Path = "/bff/logout";
    internal const string Status = "/bff/session";

    private const string Html = "text/html";
    private const string ReferrerPolicy = "Referrer-Policy";
    private const string NoReferrer = "no-referrer";
    private const string ContentSecurityPolicy = "Content-Security-Policy";
    private const string Csp = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";

    /// <summary>Requires the exact local HTML security headers independently of the implementation's constants.</summary>
    internal static void RequirePageHeaders(HttpResponseMessage response)
    {
        Assert.Equal(Html, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(NoReferrer, Assert.Single(response.Headers.GetValues(ReferrerPolicy)));
        Assert.Equal(Csp, Assert.Single(response.Headers.GetValues(ContentSecurityPolicy)));
        Assert.True(response.Headers.CacheControl?.NoStore == true);
    }

    /// <summary>Reads the real inert logout form and its native request token without logging protected content.</summary>
    internal static async Task<string> EntryAsync(HttpClient browser)
    {
        using var response = await browser.GetAsync(Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        RequirePageHeaders(response);
        return AdministratorJourney.Input(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), AdministratorJourney.Antiforgery);
    }

    /// <summary>Uses the existing exact-origin form sender without redirect following or retry.</summary>
    internal static Task<HttpResponseMessage> PostAsync(HttpClient browser, string token) => BrowserIssuerHttp.PostAsync(browser,
        Path, new Dictionary<string, string> { [AdministratorJourney.Antiforgery] = token });

    /// <summary>Requires a local-only truthful result with no issuer navigation or automatic recovery request.</summary>
    internal static async Task CommittedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        RequirePageHeaders(response);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(LocalOutcome, html, StringComparison.Ordinal);
        Assert.Contains(RemoteOutcome, html, StringComparison.Ordinal);
        Assert.DoesNotContain(ScriptElement, html, StringComparison.Ordinal);
        Assert.Null(response.Headers.Location);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
    }

    /// <summary>Requires exact anonymous projection and no cookie mutation from a retained raw cookie pair.</summary>
    internal static async Task DeniedStatusAsync(BrowserLoginHost host, Uri client, string cookies)
    {
        using var response = await BrowserCallbackTests.ReplayAsync(host, new Uri(client, Status), cookies);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AnonymousStatus, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.False(response.Headers.Contains(SetCookie));
    }
}
