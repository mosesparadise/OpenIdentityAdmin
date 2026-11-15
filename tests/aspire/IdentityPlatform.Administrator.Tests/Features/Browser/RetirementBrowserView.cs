using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Uses shipped permanent-retirement confirmation and retained-list navigation, including actual logout and fresh password/MFA.</summary>
internal static class RetirementBrowserView
{
    internal const string Completed = "Revocation completed. The application remains permanently retired with history retained.";
    internal const string Unknown = "The application may already be permanently retired and revocation accepted.";
    internal const string Suffix = "/retirement";
    internal const string Collection = "/bff/api/v1/applications/";
    private const string Applications = "/applications";
    private const string RetiredPath = "/retired-applications";
    private const string RetiredNavigation = "Retired applications";
    private const string Details = "View details for ";
    private const string Action = "Retire application";
    private const string Submit = "Permanently retire application";
    private const string Cancel = "Cancel";
    private const string Heading = "#editor-heading";
    private const string Confirmation = "#lifecycle-confirm";
    private const string Reason = "#lifecycle-reason";
    private const string Security = "security";
    private const string Enter = "Enter";
    private const string Permanent = "Permanently retired managed application";
    private const string JobReference = "Job reference: ";
    private static readonly string[] ForbiddenActions = ["Edit application", "Replace secret", "Clone application", "Re-enable application", Action, "Revoke grants"];

    /// <summary>Verifies unchecked confirmation, cancellation focus, required validation and responsive themes before one explicit send.</summary>
    internal static async Task PrepareAsync(IPage page, BrowserHttpsFixture environment, ApplicationEditorRegistration registration)
    {
        await GrantRecoveryView.OpenAsync(page, environment.Profile.ClientUri, registration);
        await Button(page, Action).ClickAsync();
        await Assertions.Expect(page.Locator(Heading)).ToBeFocusedAsync();
        Assert.False(await page.Locator(Confirmation).IsCheckedAsync());
        await Button(page, Cancel).ClickAsync();
        await Assertions.Expect(Button(page, Action)).ToBeFocusedAsync();
        await Button(page, Action).ClickAsync();
        await Button(page, Submit).PressAsync(Enter);
        await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToBeFocusedAsync();
        await GrantRecoveryView.PresentationAsync(page);
        await page.Locator(Reason).SelectOptionAsync(Security);
        await page.Locator(Confirmation).CheckAsync();
    }

    /// <summary>Sends only through the keyboard-activated shipped form.</summary>
    internal static Task SendAsync(IPage page) => Button(page, Submit).PressAsync(Enter);

    /// <summary>Requires exclusion from normal navigation and loads terminal details from the separate retained list.</summary>
    internal static async Task OpenAsync(IPage page, BrowserHttpsFixture environment, ApplicationEditorRegistration registration, Guid job)
    {
        await page.GotoAsync(new Uri(environment.Profile.ClientUri, Applications).AbsoluteUri);
        await Assertions.Expect(Button(page, Details + registration.ClientId)).ToHaveCountAsync(0);
        await page.GetByRole(AriaRole.Link, new()
        {
            Name = RetiredNavigation,
            Exact = true
        }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Uri(environment.Profile.ClientUri, RetiredPath).AbsoluteUri);
        await Button(page, Details + registration.ClientId).ClickAsync();
        await Assertions.Expect(page.GetByText(Permanent, new()
        {
            Exact = true
        })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText(JobReference + job, new()
        {
            Exact = true
        })).ToBeVisibleAsync();
        foreach (var action in ForbiddenActions)
        {
            await Assertions.Expect(Button(page, action)).ToHaveCountAsync(0);
        }
    }

    /// <summary>Destroys transient page state, performs actual issuer logout, then fresh password/MFA before recovering the same retained identity.</summary>
    internal static async Task ReloginAsync(IPage page, BrowserHttpsFixture environment, ApplicationEditorRegistration registration, Guid job)
    {
        await page.ReloadAsync();
        await OpenAsync(page, environment, registration, job);
        await BrowserHttpsJourney.LogoutAsync(environment, page, issuer: true);
        await page.GotoAsync((await BrowserLogoutJourney.LinkAsync(environment, page)).AbsoluteUri);
        await BrowserLogoutJourney.ConfirmAsync(environment, page);
        await ApplicationAccessFixture.SignInAsync(environment, page);
        await OpenAsync(page, environment, registration, job);
    }

    /// <summary>Uses exact accessible button names without injecting request keys or application state.</summary>
    private static ILocator Button(IPage page, string name) => page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
}
