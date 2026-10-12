using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Exercises all approved account-security journeys in the required theme, viewport, provider and engine matrix.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "Security")]
public sealed class SecurityBrowserTests(BootstrapFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly string[] Themes = ["light", "dark"];
    private static readonly int[] Widths = [320, 1280];
    private const string ProviderTrait = "Provider";
    private const string ThemeSelector = "#theme";

    /// <summary>Requires both real providers and every light/dark compact/desktop combination for each selected engine.</summary>
    public static IEnumerable<TheoryDataRow<string, string, int>> Cases =>
        from provider in Providers
        from theme in Themes
        from width in Widths
        select new TheoryDataRow<string, string, int>(provider, theme, width).WithTrait(ProviderTrait, provider);

    /// <summary>Completes replacement, regeneration, restricted recovery and logout using keyboard-accessible native forms and secret-safe evidence.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_CurrentAdministrator_When_AllSecurityJourneysComplete_Then_KeyboardThemeAccessibilityAndSecretHistoryHold(string provider, string theme, int width)
    {
        await AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync(width);
            var page = await session.Context.NewPageAsync();
            await page.GotoAsync(new Uri(environment.Issuer, AdministratorJourney.Enrollment).AbsoluteUri);
            await page.Locator(ThemeSelector).SelectOptionAsync(theme);
            await SecurityBrowserJourney.RunAsync(environment, page, scripted: true);
        });
    }

    /// <summary>All new state-changing actions retain their native antiforgery-protected form behavior without page JavaScript.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_DisabledPageScripts_When_AllSecurityJourneysComplete_Then_NativeFormsAndNoRedisplayRemainEffective(string provider)
    {
        await AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync(javaScript: false);
            var page = await session.Context.NewPageAsync();
            await page.GotoAsync(new Uri(environment.Issuer, AdministratorJourney.Enrollment).AbsoluteUri);
            await SecurityBrowserJourney.RunAsync(environment, page, scripted: false);
        });
    }
}
