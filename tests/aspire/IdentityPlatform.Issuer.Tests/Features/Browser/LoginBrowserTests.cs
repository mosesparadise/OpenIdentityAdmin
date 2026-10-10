using Deque.AxeCore.Playwright;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

[Collection("issuer")]
[Trait("Category", "Browser")]
public sealed class LoginBrowserTests(IssuerFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly string[] Themes = ["light", "dark"];
    private static readonly int[] Widths = [320, 1280];
    public static IEnumerable<TheoryDataRow<string, string, int>> VisualCases =>
        from provider in Providers
        from theme in Themes
        from width in Widths
        select new TheoryDataRow<string, string, int>(provider, theme, width).WithTrait("Provider", provider);

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Genuine_keyboard_login_completes_under_strict_csp_and_keeps_tokens_out_of_browser_storage(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        await using var context = await browser.NewContextAsync(); // TLS errors stay fatal.
        var page = await context.NewPageAsync();
        var violations = new List<string>();
        await page.ExposeFunctionAsync("recordCspViolation", (string directive) => violations.Add(directive));
        await page.AddInitScriptAsync("document.addEventListener('securitypolicyviolation', event => window.recordCspViolation(event.effectiveDirective));");
        var flow = environment.RelyingParty.Prepare(exchange: true);
        var response = await page.GotoAsync(environment.RelyingParty.StartUri(flow).AbsoluteUri);
        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        Assert.Equal("/account/login", new Uri(page.Url).AbsolutePath);
        var headers = await response.AllHeadersAsync();
        Assert.Contains("form-action 'self'", headers["content-security-policy"], StringComparison.Ordinal);
        Assert.Equal("no-store", headers["cache-control"]);
        Assert.Equal("no-referrer", headers["referrer-policy"]);
        await page.Keyboard.PressAsync("Tab");
        Assert.Equal("Skip to main content", await page.Locator(":focus").InnerTextAsync());
        await page.GetByLabel("Username", new()
        {
            Exact = true
        }).FillAsync(environment.Username);
        await page.GetByLabel("Password", new()
        {
            Exact = true
        }).FillAsync(environment.Password);
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = "Show password",
            Exact = true
        }).ClickAsync();
        Assert.Equal("text", await page.Locator("#password").GetAttributeAsync("type"));
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = "Hide password",
            Exact = true
        }).PressAsync("Space");
        Assert.Equal("password", await page.Locator("#password").GetAttributeAsync("type"));
        await page.Locator("#password").PressAsync("Enter");
        await page.WaitForURLAsync(environment.RelyingParty.Callback + "**");
        Assert.True(await flow.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken), flow.ValidationFailure);
        Assert.Equal("Signed in successfully", await page.Locator("body").InnerTextAsync());
        Assert.Empty(violations);
        var cookies = await context.CookiesAsync();
        var session = Assert.Single(cookies, cookie => cookie.Name == "__Host-identity-pilot");
        Assert.True(session.Secure);
        Assert.True(session.HttpOnly);
        Assert.Equal(SameSiteAttribute.Lax, session.SameSite);
        var storage = await context.StorageStateAsync();
        Assert.DoesNotContain("access_token", storage, StringComparison.Ordinal);
        Assert.DoesNotContain("id_token", storage, StringComparison.Ordinal);
        Assert.DoesNotContain(environment.Password, storage, StringComparison.Ordinal);
        Assert.DoesNotContain(environment.ClientSecret, storage, StringComparison.Ordinal);
        Assert.Empty(await page.EvaluateAsync<string[]>("Object.keys(sessionStorage)"));
    }

    [Theory]
    [MemberData(nameof(VisualCases))]
    public async Task Both_themes_reflow_and_have_no_automated_accessibility_violations(string provider, string theme, int width)
    {
        var environment = await fixture.GetAsync(provider);
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        await using var context = await browser.NewContextAsync(new()
        {
            ViewportSize = new()
            {
                Width = width,
                Height = 900
            },
            ReducedMotion = ReducedMotion.Reduce
        });
        var page = await context.NewPageAsync();
        var flow = environment.RelyingParty.Prepare();
        await page.GotoAsync(environment.RelyingParty.StartUri(flow).AbsoluteUri);
        await page.GetByLabel("Theme", new()
        {
            Exact = true
        }).SelectOptionAsync(theme);
        Assert.Equal(theme, await page.Locator("html").GetAttributeAsync("data-theme"));
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        var axe = await page.RunAxe();
        Assert.Empty(axe.Violations.Select(value => value.Id));
        var root = Environment.GetEnvironmentVariable("IDENTITY_TEST_ARTIFACTS") ?? Path.Combine("artifacts", "tests", provider);
        var path = Path.Combine(root, "screenshots", $"login-{theme}-{width}.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await page.ScreenshotAsync(new()
        {
            Path = path,
            FullPage = true,
            Animations = ScreenshotAnimations.Disabled,
            Caret = ScreenshotCaret.Hide
        });
        var keys = await page.EvaluateAsync<string[]>("Object.keys(localStorage)");
        Assert.Equal(["identity-theme"], keys);
        await page.ReloadAsync();
        Assert.Equal(theme, await page.Locator("html").GetAttributeAsync("data-theme"));
        // Screenshots are retained for explicit review; baseline acceptance is tracked separately.
    }
}
