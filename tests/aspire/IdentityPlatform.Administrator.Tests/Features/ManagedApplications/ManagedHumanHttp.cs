using System.Net;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Drives real native managed cache and Razor forms over HTTPS; no authenticator or antiforgery service is replaced.</summary>
internal static class ManagedHumanHttp
{
    internal const string Path = "/account/application/sign-in";
    internal const string Flow = "Flow";
    internal const string Decision = "Decision";
    internal const string Approve = "approve";
    internal const string Deny = "deny";
    internal const string Username = "Username";
    internal const string Password = "Password";
    internal const string Code = "Code";
    private const string ApprovedHeading = "Consent saved";
    private const string DeniedHeading = "Sign-in cancelled";
    private const string FormStart = "<form";
    private const string QueryStart = "?";
    private const string ParseOrigin = "https://issuer.example.test";
    private const string Cookie = "Set-Cookie";
    private const string AntiforgeryCookie = "__Host-pilot-antiforgery=";

    /// <summary>Follows actual native anonymous caching and restored-request publication into the first managed password page.</summary>
    internal static async Task<Dictionary<string, string>> StartAsync(ManagedCacheTestContext test)
    {
        using var initial = await test.SendAsync(ManagedCacheTestContext.Initial());
        using var resumed = await test.SendAsync(await test.ResumeAsync(initial));
        await EntryAsync(resumed);
        return await FormAsync(test.Http.IssuerClient, resumed.Headers.Location!);
    }

    /// <summary>Requires an issuer-local account redirect with no new identity cookie or body, without logging opaque form material.</summary>
    internal static async Task EntryAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(response.Headers.Location is { IsAbsoluteUri: false });
        var uri = new Uri(new Uri(ParseOrigin), response.Headers.Location!);
        Assert.True(response.Headers.Location!.OriginalString.StartsWith(Path + QueryStart, StringComparison.Ordinal) &&
            uri.GetLeftPart(UriPartial.Authority) == ParseOrigin && uri.AbsolutePath == Path && string.IsNullOrEmpty(uri.Fragment));
        var query = QueryHelpers.ParseQuery(uri.Query);
        Assert.True(query.Count == 1 && query.TryGetValue(Flow, out var value) && value.Count == 1 && !string.IsNullOrEmpty(value[0]));
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        NoAuthorityCookie(response);
    }

    /// <summary>Reads actual native form fields, verifying the same-origin security headers without retaining markup in failure output.</summary>
    internal static async Task<Dictionary<string, string>> FormAsync(HttpClient browser, Uri location)
    {
        using var page = await browser.GetAsync(location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        await BrowserFormHeaders.AccountAsync(page);
        NoAuthorityCookie(page);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return new(StringComparer.Ordinal)
        {
            [Flow] = AdministratorJourney.Input(html, Flow),
            [AdministratorJourney.Antiforgery] = AdministratorJourney.Input(html, AdministratorJourney.Antiforgery)
        };
    }

    /// <summary>Reads the real terminal Razor page and requires the decision-specific heading with no further actionable form or authority cookie.</summary>
    internal static async Task TerminalAsync(ManagedCacheTestContext test, Uri location, bool approve)
    {
        using var page = await test.Http.IssuerClient.GetAsync(location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        await BrowserFormHeaders.AccountAsync(page);
        NoAuthorityCookie(page);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(html.Contains(approve ? ApprovedHeading : DeniedHeading, StringComparison.Ordinal));
        Assert.False(html.Contains(FormStart, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Posts the actual fixture credentials only through the protected managed password form.</summary>
    internal static Task<HttpResponseMessage> PasswordAsync(ManagedCacheTestContext test, Dictionary<string, string> form, string? password = null) =>
        BrowserIssuerHttp.PostAsync(test.Http.IssuerClient, Path, new Dictionary<string, string>(form, StringComparer.Ordinal)
        {
            [Username] = test.Http.Environment.Issuer.Account.Journey.Lease.Username,
            [Password] = password ?? test.Http.Environment.Issuer.Account.Journey.Lease.Password
        });

    /// <summary>Uses an independent native-wall-clock TOTP oracle and retained real replay ledger.</summary>
    internal static async Task<HttpResponseMessage> MfaAsync(ManagedCacheTestContext test, Dictionary<string, string> form, string? code = null) =>
        await BrowserIssuerHttp.PostAsync(test.Http.IssuerClient, Path, new Dictionary<string, string>(form, StringComparer.Ordinal)
        {
            [Code] = code ?? await IssuerContinuationProviderFixture.UnusedCodeAsync(test.Http.Environment.Issuer.Account.Journey)
        });

    /// <summary>Submits explicit consent through the same actual protected form path.</summary>
    internal static Task<HttpResponseMessage> DecideAsync(ManagedCacheTestContext test, Dictionary<string, string> form) =>
        BrowserIssuerHttp.PostAsync(test.Http.IssuerClient, Path, new Dictionary<string, string>(form, StringComparer.Ordinal) { [Decision] = Approve });

    /// <summary>Allows only native antiforgery-cookie establishment; managed proof creation never returns administrator/BFF authority cookies.</summary>
    internal static void NoAuthorityCookie(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues(Cookie, out var values))
        {
            Assert.True(values.All(value => value.StartsWith(AntiforgeryCookie, StringComparison.Ordinal)));
        }
    }
}
