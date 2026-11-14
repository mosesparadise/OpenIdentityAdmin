using IdentityPlatform.Administrator.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.AppHost.Tests.Features.Samples;

/// <summary>Uses the real sample's native client, issuer forms and independently validating API over ordinarily trusted browser TLS.</summary>
internal static class SampleBrowserJourney
{
    internal const string Address = "https://sample.dev.localhost:17446/";
    private const string CookiePath = "/";
    private const string Title = "Sample application";
    private const string SignIn = "Sign in";
    private const string Password = "#password";
    private const string Username = "#username";
    private const string Submit = "#submit";
    private const string Factor = "#code";
    private const string Consent = "Review access";
    private const string Allow = "Allow sign-in";
    private const string Cancel = "Cancel";
    private const string Cancelled = "Sign-in cancelled";
    private const string Profile = "Fetch current profile";
    private const string Orders = "Call orders API";
    private const string Logout = "Sign out locally";
    private const string ProfileSuccess = "Current profile received.";
    private const string OrdersSuccess = "Orders API accepted this token: sample-order-001 is Ready.";
    private const string SignedIn = "Signed in as";
    private const string ScopeList = "#requested-scopes";
    private const string ApiScope = "orders.read";
    private const string EmailScope = "sample.email";
    private const string Enter = "Enter";
    private const string SampleCookie = "__Host-sample-session";

    /// <summary>Contrasts current issuer authority denial with offline acceptance of the same still-unexpired server-held token.</summary>
    internal static async Task ChangedPolicyAsync(IPage page)
    {
        const string denial = "Access denied.";
        const string profileValues = ".sample-profile";
        await Button(page, Profile).PressAsync(Enter);
        await page.GetByText(denial, new()
        {
            Exact = false
        }).WaitForAsync();
        Assert.Equal(0, await page.Locator(profileValues).CountAsync());
        await BrowserLoginPresentation.PageAsync(page, dark: false);
        await BrowserLoginPresentation.PageAsync(page, dark: true);
        await Button(page, Orders).PressAsync(Enter);
        await page.GetByText(OrdersSuccess, new()
        {
            Exact = true
        }).WaitForAsync();
    }

    /// <summary>Requires password and MFA even with an existing issuer cookie, then explicitly allows or cancels the visible scopes.</summary>
    internal static async Task SignInAsync(IPage page, LocalDemoRuntime runtime, Func<Task<string>> factor, bool allow)
    {
        await page.GotoAsync(Address);
        await Button(page, SignIn).PressAsync(Enter);
        await page.Locator(Username).FillAsync(LocalDemoRuntime.Username);
        await page.Locator(Password).FillAsync(runtime.Password);
        await page.Locator(Submit).PressAsync(Enter);
        await page.Locator(Factor).WaitForAsync();
        await page.Locator(Factor).FillAsync(await factor());
        await page.Locator(Submit).PressAsync(Enter);
        await page.GetByRole(AriaRole.Heading, new()
        {
            Name = Consent,
            Exact = true
        }).WaitForAsync();
        await Assertions.Expect(page.Locator(ScopeList)).ToContainTextAsync(ApiScope);
        await Assertions.Expect(page.Locator(ScopeList)).ToContainTextAsync(EmailScope);
        if (allow)
        {
            await LocalDemoNavigation.SubmitAsync(page, Button(page, Allow), Address);
            await page.GetByText(SignedIn, new()
            {
                Exact = false
            }).WaitForAsync();
            var cookie = Assert.Single(await page.Context.CookiesAsync(Address), cookie => cookie.Name == SampleCookie);
            Assert.True(cookie.HttpOnly && cookie.Secure && cookie.Path == CookiePath);
        }
        else
        {
            await Button(page, Cancel).PressAsync(Enter);
            await page.GetByRole(AriaRole.Heading, new()
            {
                Name = Cancelled,
                Exact = true
            }).WaitForAsync();
            Assert.DoesNotContain(await page.Context.CookiesAsync(Address), cookie => cookie.Name == SampleCookie);
        }
    }

    /// <summary>Fetches profile and genuine API data only through protected sample actions; validates both themes and browser storage.</summary>
    internal static async Task ResourcesAsync(IPage page, string[] secrets)
    {
        await Button(page, Profile).PressAsync(Enter);
        await page.GetByText(ProfileSuccess, new()
        {
            Exact = true
        }).WaitForAsync();
        await BrowserLoginPresentation.PageAsync(page, dark: false);
        await BrowserLoginPresentation.PageAsync(page, dark: true);
        await LongProfileLayoutAsync(page);
        await Button(page, Orders).PressAsync(Enter);
        await page.GetByText(OrdersSuccess, new()
        {
            Exact = true
        }).WaitForAsync();
        await BrowserLoginPresentation.PageAsync(page, dark: false);
        await BrowserLoginPresentation.PageAsync(page, dark: true);
        await BrowserSecretOracle.StorageAsync(page, secrets);
        Assert.Equal(1, await page.GetByRole(AriaRole.Heading, new()
        {
            Name = Title,
            Exact = true
        }).CountAsync());
    }

    /// <summary>Logs out one sample browser, then requires a second independent browser's live profile action to remain valid.</summary>
    internal static async Task LogoutAsync(IPage page)
    {
        await Button(page, Logout).PressAsync(Enter);
        await page.GotoAsync(Address);
        Assert.Equal(0, await page.GetByText(SignedIn, new()
        {
            Exact = false
        }).CountAsync());
        Assert.DoesNotContain(await page.Context.CookiesAsync(Address), cookie => cookie.Name == SampleCookie);
    }

    /// <summary>Finds visible native actions by exact accessible name for keyboard-driven operation.</summary>
    private static ILocator Button(IPage page, string name) => page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    /// <summary>Seeds only displayed text with the documented maximum-length projection to verify actual narrow-screen CSS, then restores it.</summary>
    private static async Task LongProfileLayoutAsync(IPage page)
    {
        const string values = ".sample-profile dd";
        const string replace = "nodes => { const original=nodes.map(n=>n.textContent); nodes.forEach(n=>n.textContent='x'.repeat(256)); return original; }";
        const string restore = "(nodes, values) => nodes.forEach((n,i)=>n.textContent=values[i])";
        var original = await page.Locator(values).EvaluateAllAsync<string[]>(replace);
        Assert.NotEmpty(original);
        try
        {
            await BrowserLoginPresentation.PageAsync(page, dark: false);
            await BrowserLoginPresentation.PageAsync(page, dark: true);
        }
        finally { await page.Locator(values).EvaluateAllAsync<object?>(restore, original); }
    }
}
