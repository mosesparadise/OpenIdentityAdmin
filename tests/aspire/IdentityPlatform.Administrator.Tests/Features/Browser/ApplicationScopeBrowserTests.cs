using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Accepts the actual shipped assignment UI with native authentication, persisted permission observations and retained browser accessibility gates.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class ApplicationScopeBrowserTests(BootstrapFixture fixture)
{
    private static readonly string[] Themes = ["light", "dark"];
    private const string Resource = "browser-orders-api";
    private const string Permission = "scp:browser.orders.read";
    private const string Choice = "browser.orders.read — Read updated orders";
    private const string Confirmation = "#assignment-confirm";
    private const string Heading = "#application-scopes-heading";
    private const string Save = "Save scope assignment";
    private const string Enter = "Enter";
    private const string SearchField = "#assignment-scope-search";
    private const string Search = "Search scopes";
    private const string Missing = "absent";
    private const string Selected = "Selected additional scopes (1/32)";
    private const string Blocked = "Saved assignment: sign-in and token issuance are blocked";
    private const string Empty = "Saved assignment: no additional scopes.";
    private const string Remove = "Remove browser.orders.read";
    private const string Assigned = "ApplicationScopesAssigned";

    private const string Reload = "Reload current details";
    private const string NoMatches = "No matching scopes.";
    private const string ConfirmationError = "Confirm the scope change before saving.";
    private const string Put = "PUT";
    private const string Appearance = "Appearance";
    private const int ClearedRevisionIncrement = 2;
    private const int AssignedPermissions = 5;
    private const int OriginalPermissions = 4;
    private const int CompletedAudits = 2;

    /// <summary>Real catalogue and application forms precede explicit checked assignment and removal; native rows, one-send counts, focus, storage, themes and reflow are verified.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_CurrentAdministrator_When_AssignmentUiSavesAndClears_Then_VisibleWarningsMatchNativePermissionsAndAccessibleRecovery(string provider) =>
        AdministratorBrowserEvidence.RunAsync(() => RunAsync(provider));

    /// <summary>Creates each prerequisite through production browser routes, with independently hosted API and shared current-factor clock.</summary>
    private async Task RunAsync(string provider)
    {
        var clock = new FactorClock(TimeProvider.System);
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true, managed: true, clock: clock);
        await using var browser = await BrowserSession.CreateAsync();
        var page = await browser.Context.NewPageAsync();
        await ApplicationAccessFixture.SignInAsync(environment, page);
        await CatalogueBrowserJourney.CreateAudienceAsync(page, environment.Profile.ClientUri);
        await using var observer = environment.Issuer.Account.CreateScope();
        var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var audience = await database.Set<IdentityPlatform.Infrastructure.Features.Catalogue.ApiAudience>().AsNoTracking()
            .SingleAsync(row => row.Resource == Resource, TestContext.Current.CancellationToken);
        await CatalogueBrowserJourney.CreateScopeAsync(page, environment.Profile.ClientUri, audience.Id);
        var registration = await ApplicationEditorJourney.RunAsync(page, environment.Profile.ClientUri, environment.Profile.Issuer, clock.GetUtcNow());
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = Reload,
            Exact = true
        }).ClickAsync();
        var before = await ApplicationUpdateSnapshot.ReadAsync(database, registration.Id, TestContext.Current.CancellationToken);
        await AssignAsync(page, environment.Profile.ClientUri, registration.Id);
        await RequireAsync(database, registration.Id, before.Policy.Revision + 1, true);
        await Button(page, Remove).ClickAsync();
        await Assertions.Expect(page.Locator(Heading)).ToBeFocusedAsync();
        await SaveAsync(page, environment.Profile.ClientUri, registration.Id, Empty);
        await RequireAsync(database, registration.Id, before.Policy.Revision + ClearedRevisionIncrement, false);
        await PresentationAsync(page);
        await WorkspacePresentation.RequireStorageAsync(page);
    }

    /// <summary>Search excludes the selected entry without dropping it; unchecked confirmation produces no dispatch before one explicit keyboard save.</summary>
    private static async Task AssignAsync(IPage page, Uri origin, string id)
    {
        await Assertions.Expect(page.GetByText(Empty, new()
        {
            Exact = false
        })).ToBeVisibleAsync();
        await page.GetByLabel(Choice, new()
        {
            Exact = true
        }).CheckAsync();
        await page.Locator(SearchField).FillAsync(Missing);
        await Button(page, Search).PressAsync(Enter);
        await Assertions.Expect(page.GetByText(NoMatches, new()
        {
            Exact = true
        })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new()
        {
            Name = Selected,
            Exact = true
        })).ToBeVisibleAsync();
        using var submissions = new ApplicationSubmission(page, Put, new Uri(origin, ApplicationScopeHttpTests.Path(id)).AbsoluteUri);
        await Button(page, Save).PressAsync(Enter);
        await Assertions.Expect(page.GetByText(ConfirmationError, new()
        {
            Exact = true
        })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(Heading)).ToBeFocusedAsync();
        await PresentationAsync(page);
        await page.Locator(Confirmation).CheckAsync();
        await Button(page, Save).PressAsync(Enter);
        await Assertions.Expect(page.GetByText(Blocked, new()
        {
            Exact = false
        })).ToBeVisibleAsync();
        submissions.RequireOnce();
    }

    /// <summary>Requires a single actual write and the subsequent independently read saved-state warning.</summary>
    private static async Task SaveAsync(IPage page, Uri origin, string id, string expected)
    {
        using var submissions = new ApplicationSubmission(page, Put, new Uri(origin, ApplicationScopeHttpTests.Path(id)).AbsoluteUri);
        await page.Locator(Confirmation).CheckAsync();
        await Button(page, Save).PressAsync(Enter);
        await Assertions.Expect(page.GetByText(expected, new()
        {
            Exact = false
        })).ToBeVisibleAsync();
        submissions.RequireOnce();
    }

    /// <summary>Reads actual database rows after publication and permits exactly the expected assignment, revision and audit count.</summary>
    private static async Task RequireAsync(PlatformDbContext database, string id, long revision, bool assigned)
    {
        var token = TestContext.Current.CancellationToken;
        var native = await database.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking().SingleAsync(row => row.Id == id, token);
        var permissions = JsonSerializer.Deserialize<string[]>(native.Permissions!)!;
        Assert.Equal(assigned, permissions.Contains(Permission, StringComparer.Ordinal));
        Assert.Equal(assigned ? AssignedPermissions : OriginalPermissions, permissions.Length);
        Assert.Equal(revision, (await database.Set<ApplicationPolicy>().AsNoTracking().SingleAsync(row => row.ApplicationId == id, token)).Revision);
        Assert.Equal(assigned ? 1 : CompletedAudits, await database.Set<IdentityPlatform.Infrastructure.Features.Identity.Persistence.SecurityAuditIntent>()
            .CountAsync(row => row.TargetId == id && row.Event == Assigned, token));
    }

    /// <summary>Retains shared axe, contrast, touch targets and three-width reflow checks for both supported themes.</summary>
    private static async Task PresentationAsync(IPage page)
    {
        foreach (var theme in Themes)
        {
            await page.GetByRole(AriaRole.Combobox, new()
            {
                Name = Appearance
            }).SelectOptionAsync(theme);
            await WorkspacePresentation.RequireStateAsync(page);
        }
    }

    /// <summary>Resolves the exact visible action rather than relying on implementation callbacks.</summary>
    private static ILocator Button(IPage page, string name) => page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
}
