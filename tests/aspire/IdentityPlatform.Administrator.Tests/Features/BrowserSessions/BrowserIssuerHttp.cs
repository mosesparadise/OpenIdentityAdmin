using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Drives actual native Razor forms while keeping passwords, selectors and tokens in memory and out of diagnostics.</summary>
internal static class BrowserIssuerHttp
{
    internal const string Login = "/account/administrator/browser/login";
    internal const string Mfa = "/account/administrator/browser/mfa";
    internal const string Intent = "Intent";
    internal const string Completion = "Completion";
    internal const string Username = "Username";
    internal const string Password = "Password";
    private const string Origin = "Origin";

    /// <summary>Loads the fixed entry and retains only native hidden form fields, never page content in test output.</summary>
    internal static async Task<Dictionary<string, string>> EntryAsync(HttpClient browser, string intent)
    {
        using var page = await browser.GetAsync(QueryHelpers.AddQueryString(Login, Intent, intent), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        await BrowserFormHeaders.AccountAsync(page);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return new(StringComparer.Ordinal)
        {
            [Intent] = AdministratorJourney.Input(html, Intent),
            [AdministratorJourney.Antiforgery] = AdministratorJourney.Input(html, AdministratorJourney.Antiforgery)
        };
    }

    /// <summary>Supplies actual bootstrap credentials to the native fixed password page with an exact configured Origin.</summary>
    internal static Task<HttpResponseMessage> PasswordAsync(HttpClient browser, JourneyScenario scenario, Dictionary<string, string> entry, string? password = null) =>
        PostAsync(browser, Login, new Dictionary<string, string>(entry, StringComparer.Ordinal)
        {
            [Username] = scenario.Lease.Username,
            [Password] = password ?? scenario.Lease.Password
        });

    /// <summary>Reads only the fixed admitted MFA locator, rejecting arbitrary cross-origin server redirects in the fixture.</summary>
    internal static async Task<Dictionary<string, string>> FactorAsync(HttpClient browser, HttpResponseMessage password)
    {
        Assert.Equal(HttpStatusCode.Redirect, password.StatusCode);
        BrowserFormHeaders.Referrer(password);
        var location = password.Headers.Location;
        Assert.True(location is { IsAbsoluteUri: false } && location.OriginalString.StartsWith(Mfa, StringComparison.Ordinal));
        using var page = await browser.GetAsync(location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        await BrowserFormHeaders.AccountAsync(page, mfa: true);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return new(StringComparer.Ordinal)
        {
            [Intent] = AdministratorJourney.Input(html, Intent),
            [AdministratorJourney.Interaction] = AdministratorJourney.Input(html, AdministratorJourney.Interaction),
            [AdministratorJourney.Antiforgery] = AdministratorJourney.Input(html, AdministratorJourney.Antiforgery)
        };
    }

    /// <summary>Posts one explicit native form with optional deliberate origin faults; no automatic redirect/retry is performed.</summary>
    internal static async Task<HttpResponseMessage> PostAsync(HttpClient browser, string path, IEnumerable<KeyValuePair<string, string>> fields, string? origin = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(fields) };
        request.Headers.TryAddWithoutValidation(Origin, origin ?? browser.BaseAddress!.GetLeftPart(UriPartial.Authority));
        return await browser.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
