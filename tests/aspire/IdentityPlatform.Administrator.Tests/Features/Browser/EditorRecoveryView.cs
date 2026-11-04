using System.Globalization;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Names finite editor actions and checks presentation without capturing authenticated pixels or values.</summary>
internal static class EditorRecoveryView
{
    internal const string Client = "editor-recovery-client";
    internal const string Display = "Editor recovery application";
    internal const string ChangedDisplay = "Changed recovery application";
    internal const string Applications = "/applications";
    internal const string Collection = "/bff/api/v1/applications";
    internal const string Token = Collection + "/antiforgery";
    internal const string Create = "Create application";
    internal const string Edit = "Edit application";
    internal const string Replace = "Replace secret";
    internal const string Save = "Save changes";
    internal const string Back = "Back to applications";
    internal const string Reload = "Reload current details";
    internal const string Details = "Application details";
    internal const string Problem = "Review application status";
    internal const string Secret = "#created-client-secret";
    internal const string DisplayField = "#editor-displayName";
    internal const string ExpiryField = "#editor-credentialExpiresAt";
    internal const string Confirmation = "#editor-confirmed";
    internal const string Hide = "I’ve saved it — hide secret";
    internal const string Cancel = "Cancel";
    internal const string Appearance = "Appearance";
    internal const string Heading = "#editor-heading";
    internal const string Focus = "node => node === document.activeElement";
    internal const string Enter = "Enter";
    internal const string Summary = "[role=alert]";
    internal const string Callback = "https://editor-recovery.example.test/callback";
    internal const string ViewPrefix = "View details for ";
    private const string FieldPrefix = "#editor-";
    private const string ErrorLinkPrefix = "a[href='#editor-";
    private const string ErrorLinkSuffix = "']";
    private const string ClientField = "#editor-clientId";
    private const string CallbackField = "#editor-redirectUris";
    private const string UtcFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const int LifetimeMinutes = 30;
    internal static readonly string[] Themes = ["light", "dark"];
    internal static readonly int[] Widths = [320, 768, 1280];
    internal const int Height = 900;

    /// <summary>Locates a single action by its actual accessible name.</summary>
    internal static ILocator Button(IPage page, string name) => page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    /// <summary>Checks safe heading text and programmatic focus before presentation checks move focus.</summary>
    internal static async Task HeadingAsync(IPage page, string title)
    {
        await Assertions.Expect(page.Locator(Heading)).ToHaveTextAsync(title);
        Assert.True(await page.Locator(Heading).EvaluateAsync<bool>(Focus));
    }

    /// <summary>Checks all required widths in each theme without exporting DOM, screenshots or axe payloads.</summary>
    internal static async Task PresentationAsync(IPage page)
    {
        foreach (var theme in Themes)
        {
            await ThemeAsync(page, theme);
            await WorkspacePresentation.RequireStateAsync(page);
        }
    }

    /// <summary>Selects the existing theme control rather than assigning internal application state.</summary>
    internal static Task ThemeAsync(IPage page, string theme) => page.GetByRole(AriaRole.Combobox,
        new()
        {
            Name = Appearance
        }).SelectOptionAsync(theme);

    /// <summary>Populates explicit immutable identity, callback and expiry using the shipped fields.</summary>
    internal static async Task FillCreateAsync(IPage page, DateTimeOffset now)
    {
        await page.Locator(ClientField).FillAsync(Client);
        await page.Locator(DisplayField).FillAsync(Display);
        await page.Locator(CallbackField).FillAsync(Callback);
        await FillExpiryAsync(page, now);
    }

    /// <summary>Chooses a whole-second future expiry without changing server policy or product defaults.</summary>
    internal static Task FillExpiryAsync(IPage page, DateTimeOffset now) => page.Locator(ExpiryField)
        .FillAsync(now.AddMinutes(LifetimeMinutes).ToString(UtcFormat, CultureInfo.InvariantCulture));

    /// <summary>Opens actual persisted details from the protected list; it never injects editor state.</summary>
    internal static async Task OpenAsync(IPage page, Uri origin)
    {
        await page.GotoAsync(new Uri(origin, Applications).AbsoluteUri);
        await Button(page, ViewPrefix + Client).ClickAsync();
        await HeadingAsync(page, Details);
    }

    /// <summary>Requires a focused, linked error summary and no generated output after invalid submission.</summary>
    internal static async Task ErrorsAsync(IPage page, string field)
    {
        await page.Locator(Summary).WaitForAsync();
        Assert.True(await page.Locator(Summary).EvaluateAsync<bool>(Focus));
        await PresentationAsync(page);
        await page.Locator(Summary).Locator(ErrorLinkPrefix + field + ErrorLinkSuffix).FocusAsync();
        await page.Keyboard.PressAsync(Enter);
        Assert.True(await page.Locator(FieldPrefix + field).EvaluateAsync<bool>(Focus));
        Assert.Equal(0, await page.Locator(Secret).CountAsync());
    }

    /// <summary>Requires the finite problem message without retaining text or remote problem bodies.</summary>
    internal static async Task ProblemAsync(IPage page, string message)
    {
        await HeadingAsync(page, Problem);
        Assert.True(await page.GetByText(message, new()
        {
            Exact = false
        }).IsVisibleAsync());
        Assert.Equal(0, await page.Locator(Secret).CountAsync());
        await PresentationAsync(page);
    }
}
