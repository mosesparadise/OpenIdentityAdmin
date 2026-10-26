using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Exercises real administrator pages under every required engine without retaining secret-bearing diagnostics.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "Accounts")]
public sealed class EnrollmentBrowserTests(BootstrapFixture fixture)
{
    private const string EnterKey = "Enter";

    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly string[] Themes = ["light", "dark"];
    private static readonly int[] Widths = [320, 1280];
    private const string ProviderTrait = "Provider";
    private const string Username = "#username";
    private const string Password = "#password";
    private const string Submit = "#submit";
    private const string SetupKey = "#setup-key";
    private const string Code = "#code";
    private const string RecoveryCodes = ".recovery-codes code";
    private const string Theme = "#theme";
    private const string SavedLink = "I have saved my codes";
    private const string NoOverflow = "document.documentElement.scrollWidth <= window.innerWidth";
    private const string Storage = "JSON.stringify({local: {...localStorage}, session: {...sessionStorage}})";
    private const string SafeLayout = "The account page overflowed its viewport.";
    private const string Root = "html";
    private const string ThemeAttribute = "data-theme";
    private const string InvalidPassword = "incorrect-synthetic-password";
    private const string NewSignIn = "Start a new sign-in";
    private const string ErrorSummary = "#error-summary";
    private const string FocusedError = "document.activeElement?.id === 'error-summary'";
    private const string FocusFailure = "The generic account error was not focused.";
    private const string CookieFailure = "Expected exactly one protected administrator session cookie.";
    private const string NoSecretStorage = "Administrator secret material entered browser storage.";

    /// <summary>Defines both providers and the independent light/dark, compact/desktop acceptance matrix.</summary>
    public static IEnumerable<TheoryDataRow<string, string, int>> Cases =>
        from provider in Providers
        from theme in Themes
        from width in Widths
        select new TheoryDataRow<string, string, int>(provider, theme, width).WithTrait(ProviderTrait, provider);

    /// <summary>Completes native random-key enrollment and checks every secret-bearing stage for accessibility and bounded layout.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_ProtectedBootstrap_When_KeyboardEnrollmentCompletes_Then_ThemeAccessibilityAndSecretIsolationHold(string provider, string theme, int width)
    {
        await AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync(width);
            var page = await session.Context.NewPageAsync();
            var response = await page.GotoAsync(new Uri(environment.Issuer, AdministratorJourney.Enrollment).AbsoluteUri);
            Assert.NotNull(response);
            Assert.Equal(200, response.Status);
            await page.Locator(Theme).SelectOptionAsync(theme);
            Assert.Equal(theme, await page.Locator(Root).GetAttributeAsync(ThemeAttribute));
            await RequirePageAsync(page);
            await AdministratorKeyboard.PasswordAsync(page, environment.Database.Username, environment.Database.Password);
            await page.Locator(SetupKey).WaitForAsync();
            var seed = await page.Locator(SetupKey).InputValueAsync();
            await RequirePageAsync(page);
            var acceptedCode = FactorOracle.Code(seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            await AdministratorKeyboard.FactorAsync(page, acceptedCode);
            await page.Locator(RecoveryCodes).First.WaitForAsync();
            Assert.Equal(10, await page.Locator(RecoveryCodes).CountAsync());
            await RequirePageAsync(page);
            await RequireSecretIsolationAsync(page, session.Context, seed);
            await AdministratorKeyboard.AcknowledgeAsync(page);
            await page.WaitForURLAsync(new Uri(environment.Issuer, AdministratorJourney.Security).AbsoluteUri);
            await RequirePageAsync(page);
            await RequireHistoryAsync(page, seed);
            await LoginAsync(environment, session.Context, page, seed, acceptedCode);
        });
    }

    /// <summary>Completes secret-bearing enrollment using only native forms when page scripting is disabled.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_DisabledPageScripts_When_EnrollmentUsesNativeForms_Then_RecoveryCodesAndAccountStatusRemainAvailable(string provider)
    {
        await AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync(javaScript: false);
            var page = await session.Context.NewPageAsync();
            await page.GotoAsync(new Uri(environment.Issuer, AdministratorJourney.Enrollment).AbsoluteUri);
            await page.Locator(Username).FillAsync(environment.Database.Username);
            await page.Locator(Password).FillAsync(environment.Database.Password);
            await page.Locator(Submit).ClickAsync();
            await page.Locator(SetupKey).WaitForAsync();
            var seed = await page.Locator(SetupKey).InputValueAsync();
            await page.Locator(Code).FillAsync(FactorOracle.Code(seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            await page.Locator(Submit).ClickAsync();
            await page.Locator(RecoveryCodes).First.WaitForAsync();
            Assert.Equal(10, await page.Locator(RecoveryCodes).CountAsync());
            await RequireSecretIsolationAsync(page, session.Context, seed);
            using (await EnrollmentNavigationObservation.StartAsync(page,
                new Uri(environment.Issuer, AdministratorJourney.Security), SavedLink))
            {
                await page.GetByRole(AriaRole.Link, new()
                {
                    Name = SavedLink
                }).ClickAsync();
                await page.WaitForURLAsync(new Uri(environment.Issuer, AdministratorJourney.Security).AbsoluteUri);
            }
            await RequireHistoryAsync(page, seed);
        });
    }

    /// <summary>Exercises native reload and history without recording response HTML or secret values in assertion output.</summary>
    private static async Task RequireHistoryAsync(IPage page, string seed)
    {
        await AdministratorHistory.RequireNoRedisplayAsync(page, seed);
        await AdministratorHistory.ObserveAsync(page, seed, () => page.GoBackAsync());
        await AdministratorHistory.ObserveAsync(page, seed, () => page.ReloadAsync());
        await AdministratorHistory.ObserveAsync(page, seed, () => page.GoForwardAsync());
    }

    /// <summary>Uses a fresh browser binding, corrects a generic password error and completes ordinary native MFA login.</summary>
    private static async Task LoginAsync(AdministratorEnvironment environment, IBrowserContext context, IPage page, string seed, string acceptedCode)
    {
        await context.ClearCookiesAsync();
        await page.GotoAsync(new Uri(environment.Issuer, AdministratorJourney.Login).AbsoluteUri);
        await page.Locator(Username).FillAsync(environment.Database.Username);
        await page.Locator(Password).FillAsync(InvalidPassword);
        await page.Locator(Submit).PressAsync(EnterKey);
        await page.Locator(ErrorSummary).WaitForAsync();
        Assert.True(await page.EvaluateAsync<bool>(FocusedError), FocusFailure);
        await RequirePageAsync(page);
        await page.Locator(Username).FillAsync(environment.Database.Username);
        await page.Locator(Password).FillAsync(environment.Database.Password);
        await page.Locator(Submit).PressAsync(EnterKey);
        await page.Locator(Code).WaitForAsync();
        await RequirePageAsync(page);
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var invalidCode = FactorOracle.InvalidCode(seed);
        await page.Locator(Code).FillAsync(invalidCode);
        await page.Locator(Submit).PressAsync(EnterKey);
        await page.Locator(ErrorSummary).WaitForAsync();
        FactorOracle.RequireWindow(seed, invalidCode, started, valid: false);
        Assert.True(await page.EvaluateAsync<bool>(FocusedError), FocusFailure);
        await RequirePageAsync(page);
        await page.GetByRole(AriaRole.Link, new()
        {
            Name = NewSignIn
        }).ClickAsync();
        await page.Locator(Username).FillAsync(environment.Database.Username);
        await page.Locator(Password).FillAsync(environment.Database.Password);
        await page.Locator(Submit).PressAsync(EnterKey);
        await page.Locator(Code).WaitForAsync();
        await page.Locator(Code).FillAsync(FactorOracle.UnusedCode(seed, acceptedCode));
        await page.Locator(Submit).PressAsync(EnterKey);
        await page.WaitForURLAsync(new Uri(environment.Issuer, AdministratorJourney.Security).AbsoluteUri);
        await RequirePageAsync(page);
    }

    /// <summary>Runs the existing allowlisted axe diagnostics without a screenshot, trace, HTML dump or failure-capture callback.</summary>
    private static async Task RequirePageAsync(IPage page)
    {
        Assert.True(await page.EvaluateAsync<bool>(NoOverflow), SafeLayout);
        await BrowserAccessibility.RequireNoViolationsAsync(page);
    }

    /// <summary>Checks secrets only in memory and verifies that the full-session cookie remains inaccessible to JavaScript.</summary>
    private static async Task RequireSecretIsolationAsync(IPage page, IBrowserContext context, string seed)
    {
        var storage = await page.EvaluateAsync<string>(Storage);
        var codes = await page.Locator(RecoveryCodes).AllTextContentsAsync();
        Assert.False(storage.Contains(seed, StringComparison.Ordinal) || codes.Any(code => storage.Contains(code, StringComparison.Ordinal)), NoSecretStorage);
        var cookies = await context.CookiesAsync();
        var sessions = cookies.Where(cookie => cookie.Name == AdministratorJourney.SessionCookie).ToArray();
        Assert.True(sessions.Length == 1, CookieFailure);
        var session = sessions[0];
        Assert.True(session.Secure && session.HttpOnly && session.SameSite == SameSiteAttribute.Lax);
        Assert.Equal(-1, session.Expires);
    }
}
