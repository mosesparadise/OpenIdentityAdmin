using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Exercises catalogue forms, reference feedback and durable deletion through shipped browser controls and native keyboard actions.</summary>
internal static class CatalogueBrowserJourney
{
    private const string Audiences = "/audiences";
    private const string Scopes = "/scopes";
    private const string Resource = "browser-orders-api";
    private const string Name = "browser.orders.read";
    private const string AudienceName = "Browser orders";
    private const string ScopeName = "Read browser orders";
    private const string UpdatedScope = "Read updated orders";
    private const string Owner = "Operations";
    private const string Consent = "Read your order history";
    private const string Heading = "#catalogue-heading";
    private const string NameField = "#catalogue-name";
    private const string DisplayField = "#catalogue-displayName";
    private const string OwnerField = "#catalogue-owner";
    private const string ConsentField = "#catalogue-consentText";
    private const string AudienceField = "#catalogue-audienceId";
    private const string SearchField = "#catalogue-audienceSearch";
    private const string KindField = "#catalogue-kind";
    private const string KindErrorLink = "a[href='#catalogue-kind']";
    private const string Email = "email";
    private const string SelectedOption = "option:checked";
    private const string RetainedSelection = "selected; outside these search results";
    private const string ReadOnlyAttribute = "readonly";
    private const string Search = "Search audiences";
    private const string NoMatch = "absent";
    private const string CreateAudience = "Create audience";
    private const string CreateScope = "Create scope";
    private const string AudienceDetails = "Audience details";
    private const string ScopeDetails = "Scope details";
    private const string EditScope = "Edit scope";
    private const string Save = "Save changes";
    private const string View = "View ";
    private const string DeleteScope = "Delete scope";
    private const string DeleteAudience = "Delete audience";
    private const string Confirm = "Confirm deletion";
    private const string ScopeConfirmation = "I confirm deletion of this scope.";
    private const string AudienceConfirmation = "I confirm deletion of this audience.";
    private const string Completed = "Deletion completed";
    private const string Queued = "Deletion queued";
    private const string CheckStatus = "Check deletion status";
    private const string BackScopes = "Back to scopes";
    private const string Enter = "Enter";
    private const string Appearance = "Appearance";
    private static readonly string[] Themes = ["light", "dark"];
    private const int CompletionMilliseconds = 30000;

    /// <summary>Requires local linked-field validation, keyboard submission and actual resulting details before selecting this audience in a scope.</summary>
    internal static async Task CreateAudienceAsync(IPage page, Uri origin)
    {
        await page.GotoAsync(new Uri(origin, Audiences).AbsoluteUri);
        await Button(page, CreateAudience).ClickAsync();
        await Button(page, CreateAudience).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToBeFocusedAsync();
        await page.Locator(NameField).FillAsync(Resource);
        await page.Locator(DisplayField).FillAsync(AudienceName);
        await page.Locator(OwnerField).FillAsync(Owner);
        await PresentationAsync(page);
        await Button(page, CreateAudience).PressAsync(Enter);
        await RequireHeadingAsync(page, AudienceDetails);
    }

    /// <summary>A search excluding the chosen audience must retain an explicit selected option and submit that exact visible selection.</summary>
    internal static async Task CreateScopeAsync(IPage page, Uri origin, string audience)
    {
        await page.GotoAsync(new Uri(origin, Scopes).AbsoluteUri);
        await Button(page, CreateScope).ClickAsync();
        await page.Locator(NameField).FillAsync(Name);
        await page.Locator(DisplayField).FillAsync(ScopeName);
        await page.Locator(ConsentField).FillAsync(Consent);
        await page.Locator(AudienceField).SelectOptionAsync(audience);
        await RequireEmailErrorAsync(page);
        await page.Locator(SearchField).FillAsync(NoMatch);
        await Button(page, Search).ClickAsync();
        await Assertions.Expect(page.Locator(AudienceField)).ToHaveValueAsync(audience);
        await Assertions.Expect(page.Locator(AudienceField).Locator(SelectedOption)).ToContainTextAsync(RetainedSelection);
        await PresentationAsync(page);
        await Button(page, CreateScope).PressAsync(Enter);
        await RequireHeadingAsync(page, ScopeDetails);
        await Button(page, EditScope).ClickAsync();
        await Assertions.Expect(page.Locator(NameField)).ToHaveAttributeAsync(ReadOnlyAttribute, string.Empty);
        await page.Locator(DisplayField).FillAsync(UpdatedScope);
        await Button(page, Save).PressAsync(Enter);
        await RequireHeadingAsync(page, ScopeDetails);
        await Assertions.Expect(page.GetByText(UpdatedScope, new()
        {
            Exact = true
        })).ToBeVisibleAsync();
    }

    /// <summary>An email/API policy conflict must link to a visible editable type control and support keyboard recovery before any server write.</summary>
    private static async Task RequireEmailErrorAsync(IPage page)
    {
        await page.Locator(NameField).FillAsync(Email);
        await Button(page, CreateScope).PressAsync(Enter);
        await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToBeFocusedAsync();
        await page.Locator(KindErrorLink).PressAsync(Enter);
        await Assertions.Expect(page.Locator(KindField)).ToBeFocusedAsync();
        await page.Locator(NameField).FillAsync(Name);
    }

    /// <summary>Native scope mapping makes the audience visibly referenced and prevents the UI from offering a deletion confirmation.</summary>
    internal static async Task RequireReferencedAsync(IPage page, Uri origin)
    {
        await page.GotoAsync(new Uri(origin, Audiences).AbsoluteUri);
        await Button(page, View + AudienceName).ClickAsync();
        await RequireHeadingAsync(page, AudienceDetails);
        await Assertions.Expect(Button(page, DeleteAudience)).ToBeDisabledAsync();
        await PresentationAsync(page);
    }

    /// <summary>Explicit checked confirmation is followed by actual Worker completion, with focus and both-theme accessibility retained.</summary>
    internal static async Task DeleteAsync(IPage page, Uri origin, bool scope, Func<Task>? start = null)
    {
        await page.GotoAsync(new Uri(origin, scope ? Scopes : Audiences).AbsoluteUri);
        await Button(page, View + (scope ? UpdatedScope : AudienceName)).ClickAsync();
        await Button(page, scope ? DeleteScope : DeleteAudience).ClickAsync();
        await Assertions.Expect(Button(page, Confirm)).ToBeDisabledAsync();
        await PresentationAsync(page);
        await page.GetByLabel(scope ? ScopeConfirmation : AudienceConfirmation, new()
        {
            Exact = true
        }).CheckAsync();
        await Button(page, Confirm).PressAsync(Enter);
        if (start is not null)
        {
            await Assertions.Expect(page.Locator(Heading)).ToHaveTextAsync(Queued);
            await Button(page, CheckStatus).PressAsync(Enter);
            await Assertions.Expect(page.Locator(Heading)).ToBeFocusedAsync();
            await Button(page, BackScopes).FocusAsync();
            await start();
        }
        await Assertions.Expect(page.Locator(Heading)).ToHaveTextAsync(Completed, new()
        {
            Timeout = CompletionMilliseconds
        });
        await Assertions.Expect(start is null ? page.Locator(Heading) : Button(page, BackScopes)).ToBeFocusedAsync();
        await PresentationAsync(page);
    }

    /// <summary>Uses existing shared contrast, touch target, three-width reflow and native axe controls without persisting authenticated pixels.</summary>
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

    /// <summary>Requires exact finite heading and focus after asynchronous catalogue transitions.</summary>
    private static async Task RequireHeadingAsync(IPage page, string title)
    {
        await Assertions.Expect(page.Locator(Heading)).ToHaveTextAsync(title);
        await Assertions.Expect(page.Locator(Heading)).ToBeFocusedAsync();
    }

    /// <summary>Resolves only the exact visible user action, keeping action selection independent of implementation handler names.</summary>
    private static ILocator Button(IPage page, string name) => page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
}
