using Deque.AxeCore.Playwright;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

[Collection("issuer")]
[Trait("Category", "Browser")]
public sealed class BrowserAcceptanceTests(IssuerFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly string[] Themes = ["light", "dark"];
    private static readonly string[] ThemeModes = ["system", "saved", "invalid", "unavailable"];
    private static readonly string[] FreshnessModes = ["prompt-login", "max-age-zero"];
    public static IEnumerable<TheoryDataRow<string, string>> ThemeCases =>
        from provider in Providers from mode in ThemeModes select new TheoryDataRow<string, string>(provider, mode).WithTrait("Provider", provider);
    public static IEnumerable<TheoryDataRow<string, string>> FreshnessCases =>
        from provider in Providers from mode in FreshnessModes select new TheoryDataRow<string, string>(provider, mode).WithTrait("Provider", provider);
    public static IEnumerable<TheoryDataRow<string, string, int>> TerminalCases =>
        from provider in Providers
        from theme in Themes
        select new TheoryDataRow<string, string, int>(provider, theme, theme == "light" ? 320 : 1280).WithTrait("Provider", provider);

    [Theory]
    [MemberData(nameof(ThemeCases))]
    public async Task Theme_is_available_before_body_render_and_survives_storage_and_system_changes(string provider, string mode)
    {
        await BrowserEvidence.RunAsync(async () =>
        {
            var environment = await fixture.GetAsync(provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            var scriptErrors = 0;
            page.PageError += (_, _) => scriptErrors++;
            await page.EmulateMediaAsync(new()
            {
                ColorScheme = ColorScheme.Light
            });
            await page.AddInitScriptAsync(mode switch
            {
                "saved" => "localStorage.setItem('identity-theme', 'dark');",
                "invalid" => "localStorage.setItem('identity-theme', 'unrecognized');",
                "unavailable" => "Storage.prototype.getItem = Storage.prototype.setItem = () => { throw new DOMException('Storage unavailable', 'SecurityError'); };",
                _ => "localStorage.removeItem('identity-theme');"
            });
            await page.AddInitScriptAsync("""
                const observer = new MutationObserver(() => {
                    if (document.body) {
                        window.themeAtBody = document.documentElement.getAttribute('data-theme') || 'system';
                        observer.disconnect();
                    }
                });
                observer.observe(document, {childList: true, subtree: true});
                """);
            var flow = environment.RelyingParty.Prepare(exchange: true);
            await page.GotoAsync(environment.RelyingParty.StartUri(flow).AbsoluteUri);
            Assert.Equal(mode == "saved" ? "dark" : "system", await page.EvaluateAsync<string>("window.themeAtBody"));
            Assert.Equal(mode == "saved" ? "rgb(2, 6, 23)" : "rgb(248, 250, 252)", await page.Locator("body").EvaluateAsync<string>("element => getComputedStyle(element).backgroundColor"));
            await page.EmulateMediaAsync(new()
            {
                ColorScheme = ColorScheme.Dark
            });
            await Assertions.Expect(page.Locator("body")).ToHaveCSSAsync("background-color", "rgb(2, 6, 23)");
            await page.Locator("#theme").FocusAsync();
            await page.Locator("#theme").SelectOptionAsync("light");
            await Assertions.Expect(page.Locator("#theme")).ToBeFocusedAsync();
            await Assertions.Expect(page.Locator("body")).ToHaveCSSAsync("background-color", "rgb(248, 250, 252)");
            if (mode == "unavailable")
            {
                await BrowserJourneys.LoginAsync(page, environment, flow);
            }
            else
            {
                await page.Locator("#theme").SelectOptionAsync("system");
                await page.EmulateMediaAsync(new()
                {
                    ColorScheme = ColorScheme.Light
                });
                await Assertions.Expect(page.Locator("body")).ToHaveCSSAsync("background-color", "rgb(248, 250, 252)");
            }
            Assert.Equal(0, scriptErrors);
        });
    }

    [Theory]
    [MemberData(nameof(FreshnessCases))]
    public async Task Freshness_requires_new_credentials_and_completes_once_without_a_loop(string provider, string mode)
    {
        await BrowserEvidence.RunAsync(async () =>
        {
            var environment = await fixture.GetAsync(provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            var original = environment.RelyingParty.Prepare(exchange: true);
            await page.GotoAsync(environment.RelyingParty.StartUri(original).AbsoluteUri);
            await BrowserJourneys.LoginAsync(page, environment, original);
            var next = environment.RelyingParty.Prepare(exchange: true, prompt: mode == "prompt-login" ? "login" : null, maximumAge: mode == "max-age-zero" ? "0" : null);
            var response = await page.GotoAsync(environment.RelyingParty.StartUri(next).AbsoluteUri);
            Assert.Equal(200, response!.Status);
            Assert.Equal("/account/login", new Uri(page.Url).AbsolutePath);
            Assert.True(!next.Completion.Task.IsCompleted && next.Code.Length == 0, "Freshness must not issue a code before credential entry.");
            await BrowserJourneys.LoginAsync(page, environment, next);
            Assert.True(original.InteractionId != next.InteractionId && original.ExpectedSessionId != next.ExpectedSessionId, "Fresh credentials must create a new bound interaction and session.");
            Assert.True(next.ExpectedAuthenticatedAt >= original.ExpectedAuthenticatedAt, "The new credential event cannot precede the original session.");
            await BrowserJourneys.RequireNoTokensAsync(page, session.Context, environment, original, next);
        });
    }

    [Theory]
    [MemberData(nameof(TerminalCases))]
    public async Task Expired_owned_interaction_shows_safe_terminal_ui_and_allows_a_fresh_start(string provider, string theme, int width)
    {
        await BrowserEvidence.RunAsync(async () =>
        {
            var environment = await fixture.GetAsync(provider);
            await using var session = await BrowserSession.CreateAsync(width);
            var page = await session.Context.NewPageAsync();
            var expired = environment.RelyingParty.Prepare();
            await page.GotoAsync(environment.RelyingParty.StartUri(expired).AbsoluteUri);
            await page.Locator("#theme").SelectOptionAsync(theme);
            var id = await page.Locator("input[name=Interaction]").InputValueAsync();
            await using (var scope = environment.Services.CreateAsyncScope())
            {
                var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                Assert.Equal(1, await database.Set<LoginInteraction>().Where(value => value.Id == id)
                    .ExecuteUpdateAsync(update => update.SetProperty(value => value.ExpiresAt, 1), TestContext.Current.CancellationToken));
            }
            Assert.Equal(400, (await page.ReloadAsync())!.Status);
            Assert.Equal("Start a new sign-in", await page.GetByRole(AriaRole.Heading, new()
            {
                Level = 1
            }).InnerTextAsync());
            Assert.Equal(0, await page.Locator("form").CountAsync());
            Assert.True(expired.Code.Length == 0, "Expired interaction issued a code.");
            Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
            Assert.Empty((await page.RunAxe()).Violations.Select(value => value.Id));
            await BrowserVisuals.CaptureAsync(page, $"unavailable-{theme}-{width}");
            var fresh = environment.RelyingParty.Prepare(exchange: true);
            await page.GotoAsync(environment.RelyingParty.StartUri(fresh).AbsoluteUri);
            await BrowserJourneys.LoginAsync(page, environment, fresh);
        });
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Generic_credential_error_clears_password_and_supports_keyboard_correction(string provider)
    {
        await BrowserEvidence.RunAsync(async () =>
        {
            var environment = await fixture.GetAsync(provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            var flow = environment.RelyingParty.Prepare(exchange: true);
            await page.GotoAsync(environment.RelyingParty.StartUri(flow).AbsoluteUri);
            await page.Locator("#username").FillAsync(environment.Username);
            await page.Locator("#password").FillAsync("synthetic-invalid-password");
            await page.Locator("#password").PressAsync("Enter");
            await LoginBrowserTests.RequireGenericErrorAsync(page);
            Assert.True(!(await page.ContentAsync()).Contains("synthetic-invalid-password", StringComparison.Ordinal), "Rejected password was echoed in the page.");
            Assert.True(flow.Code.Length == 0 && !(await session.Context.CookiesAsync()).Any(cookie => cookie.Name == "__Host-identity-pilot"), "Credential denial must not authenticate the browser.");
            await page.Keyboard.PressAsync("Tab");
            await Assertions.Expect(page.Locator("#username")).ToBeFocusedAsync();
            await BrowserJourneys.LoginAsync(page, environment, flow);
            await BrowserJourneys.RequireNoTokensAsync(page, session.Context, environment, flow);
        });
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Login_without_javascript_uses_the_same_protected_form_and_manual_continuation(string provider)
    {
        await BrowserEvidence.RunAsync(async () =>
        {
            var environment = await fixture.GetAsync(provider);
            await using var session = await BrowserSession.CreateAsync(javaScript: false);
            var page = await session.Context.NewPageAsync();
            var flow = environment.RelyingParty.Prepare(exchange: true);
            await page.GotoAsync(environment.RelyingParty.StartUri(flow).AbsoluteUri);
            await Assertions.Expect(page.Locator("#theme-control")).ToBeHiddenAsync();
            await Assertions.Expect(page.Locator("#show-password")).ToBeHiddenAsync();
            await BrowserJourneys.LoginAsync(page, environment, flow, javaScript: false);
        });
    }
}
