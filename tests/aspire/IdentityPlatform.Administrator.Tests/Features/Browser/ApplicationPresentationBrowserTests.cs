using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Exercises React paging, focus, live permission/failure states, appearance and history boundaries on actual protected collections.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class ApplicationPresentationBrowserTests(BootstrapFixture fixture)
{
    private const string Applications = "/applications";
    private const string Registered = "Registered applications";
    private const string Unavailable = "Applications are unavailable";
    private const string Forbidden = "Application access is restricted";
    private const string Expired = "Sign in to view applications";
    private const string Enter = "Enter";
    private const string Next = "Next";
    private const string Previous = "Previous";
    private const string Reload = "Reload applications";
    private const string Search = "Search";
    private const string Prefix = "Client ID starts with";
    private const string Appearance = "Appearance";
    private const string Permission = "applications.read";
    private const int FirstPageCount = 25;
    private const int LastPageCount = 2;
    private const string PenultimateClient = "visible-application-025";
    private const string LastClient = "visible-application-026";
    private const string Rows = "tbody tr";
    private const string Focus = "() => document.activeElement?.id === 'application-state'";
    private const string PageHide = "() => window.dispatchEvent(new PageTransitionEvent('pagehide', {persisted:true}))";
    private const string PageShow = "() => window.dispatchEvent(new PageTransitionEvent('pageshow', {persisted:true}))";
    private static readonly string[] Themes = ["light", "dark"];
    private const int ExtraApplications = 26;
    private const string ClientPrefix = "visible-application-";
    private const string NumberFormat = "D3";

    /// <summary>Actual lookahead pages restore keyboard focus and remain accessible in both themes and narrow layouts; denied data is cleared.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_RealPagedCollection_When_NavigatedAndPermissionRemoved_Then_AccessibleStatesClearProtectedData(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            var probe = new ApplicationProxyProbe();
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true, applicationProbe: probe);
            await SeedAsync(environment);
            await using var browser = await BrowserSession.CreateAsync();
            var page = await browser.Context.NewPageAsync();
            await ApplicationAccessFixture.SignInAsync(environment, page);
            await page.GotoAsync(new Uri(environment.Profile.ClientUri, Applications).AbsoluteUri);
            await HeadingAsync(page, Registered);
            Assert.Equal(25, await page.Locator(Rows).CountAsync());
            await RequirePagingAsync(page);
            foreach (var theme in Themes)
            {
                await page.GetByRole(AriaRole.Combobox, new()
                {
                    Name = Appearance
                }).SelectOptionAsync(theme);
                await WorkspacePresentation.RequireStateAsync(page);
            }
            await RequireRecoveryAsync(page, probe);
            await RequirePermissionAsync(environment, page);
            await BrowserHttpsJourney.LogoutAsync(environment, page, issuer: false);
            await page.GotoAsync(new Uri(environment.Profile.ClientUri, Applications).AbsoluteUri);
            await HeadingAsync(page, Expired);
            Assert.Equal(0, await page.Locator(Rows).CountAsync());
            await WorkspacePresentation.RequireStorageAsync(page);
        });

    /// <summary>Keyboard pagination waits for actual changed rows and checks the exact final-page client identifiers before checking focus.</summary>
    private static async Task RequirePagingAsync(IPage page)
    {
        await Button(page, Next).FocusAsync();
        await page.Keyboard.PressAsync(Enter);
        await HeadingAsync(page, Registered);
        await Assertions.Expect(page.Locator(Rows)).ToHaveCountAsync(LastPageCount);
        Assert.True(await page.GetByRole(AriaRole.Cell, new()
        {
            Name = PenultimateClient,
            Exact = true
        }).First.IsVisibleAsync());
        Assert.True(await page.GetByRole(AriaRole.Cell, new()
        {
            Name = LastClient,
            Exact = true
        }).First.IsVisibleAsync());
        Assert.True(await Button(page, Next).IsDisabledAsync());
        Assert.True(await page.EvaluateAsync<bool>(Focus));
        await Button(page, Previous).FocusAsync();
        await page.Keyboard.PressAsync(Enter);
        await Assertions.Expect(page.Locator(Rows)).ToHaveCountAsync(FirstPageCount);
        await HeadingAsync(page, Registered);
    }

    /// <summary>Malformed upstream data clears the actual table and leaves the form usable for a fresh correction.</summary>
    private static async Task RequireRecoveryAsync(IPage page, ApplicationProxyProbe probe)
    {
        probe.Fault = ApplicationProxyFault.Malformed;
        await Button(page, Reload).ClickAsync();
        await HeadingAsync(page, Unavailable);
        Assert.Equal(0, await page.Locator(Rows).CountAsync());
        Assert.True(await page.GetByLabel(Prefix, new()
        {
            Exact = true
        }).IsVisibleAsync());
        Assert.True(await page.EvaluateAsync<bool>(Focus));
        probe.Fault = ApplicationProxyFault.None;
        await Button(page, Search).ClickAsync();
        await HeadingAsync(page, Registered);
        await page.EvaluateAsync(PageHide);
        Assert.Equal(0, await page.Locator(Rows).CountAsync());
        await page.EvaluateAsync(PageShow);
        await HeadingAsync(page, Registered);
    }

    /// <summary>A real committed permission removal replaces the current table with an explicit forbidden state.</summary>
    private static async Task RequirePermissionAsync(BrowserHttpsFixture environment, IPage page)
    {
        await using var scope = environment.Issuer.Account.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await database.RoleClaims.Where(row => row.ClaimValue == Permission).ExecuteDeleteAsync(TestContext.Current.CancellationToken) > 0);
        await Button(page, Reload).ClickAsync();
        await HeadingAsync(page, Forbidden);
        Assert.Equal(0, await page.Locator(Rows).CountAsync());
        await WorkspacePresentation.RequireStateAsync(page);
    }

    /// <summary>Seeds genuine native registrations with nonsecret labels so paging cannot be satisfied by generated UI placeholders.</summary>
    private static async Task SeedAsync(BrowserHttpsFixture environment)
    {
        await using var scope = environment.Issuer.Account.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        foreach (var number in Enumerable.Range(1, ExtraApplications))
        {
            var client = ClientPrefix + number.ToString(NumberFormat, System.Globalization.CultureInfo.InvariantCulture);
            database.Add(new OpenIddictEntityFrameworkCoreApplication { Id = Guid.NewGuid().ToString(), ClientId = client, DisplayName = client });
        }
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Locates exactly one accessible visible action.</summary>
    private static ILocator Button(IPage page, string name) => page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
    /// <summary>Waits for a finite user-facing result without including response data in diagnostics.</summary>
    private static Task HeadingAsync(IPage page, string name) => page.GetByRole(AriaRole.Heading, new() { Name = name, Exact = true }).WaitForAsync();
}
