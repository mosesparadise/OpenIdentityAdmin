using System.Globalization;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Shares genuine shipped UI creation, independent stale editing and one-time credential recovery with retained browsers and native Aspire demos.</summary>
internal static class ApplicationEditorJourney
{
    private const string Applications = "/applications";
    private const string CollectionPath = "/bff/api/v1/applications";
    private const string Post = "POST";
    private const string Put = "PUT";
    private const string Precondition = "if-match";
    private const string ItemSeparator = "/";
    private const int CreatedStatus = 201;
    private const int UpdatedStatus = 200;
    private const string Client = "browser-managed-editor";
    private const string Display = "Browser managed application";
    private const string Updated = "Updated browser application";
    private const string Callback = "https://browser-managed.example.test/callback";
    private const string UpdatedCallback = "https://browser-managed.example.test/new-callback";
    private const string ExpiryFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const string Heading = "#editor-heading";
    private const string Secret = "#created-client-secret";
    private const string Hide = "I’ve saved it — hide secret";
    private const string Focus = "() => document.activeElement?.id === 'editor-heading'";
    private const string HidePage = "() => window.dispatchEvent(new PageTransitionEvent('pagehide', {persisted:true}))";
    private const int LifetimeMinutes = 30;
    private const int SecretLength = 44;

    private const string EditAction = "Edit application";
    private const string DetailsTitle = "Application details";
    private const string StaleDraft = "Stale draft";
    private const string SaveAction = "Save changes";
    private const string ProblemTitle = "Review application status";
    private const string StaleMessage = "This version is no longer current.";
    private const string ReloadAction = "Reload current details";
    private const string CreateAction = "Create application";
    private const string ClientField = "#editor-clientId";
    private const string DisplayField = "#editor-displayName";
    private const string CallbacksField = "#editor-redirectUris";
    private const string ExpiryField = "#editor-credentialExpiresAt";
    private const string SummaryFocus = "node => node === document.activeElement";
    private const string EnterKey = "Enter";
    private const string CreatedTitle = "Application created";
    private const string DetailsPrefix = "View details for ";
    private const string UpdatedTitle = "Application updated";
    private const string ReplaceAction = "Replace secret";
    private const string ConfirmationField = "#editor-confirmed";
    private const string ReplacedTitle = "Secret replaced";
    private const string LightTheme = "light";
    private const string DarkTheme = "dark";
    private const string Appearance = "Appearance";
    private const string ConfirmationLabel = "Confirm replacement";
    private const string SpaceKey = "Space";
    /// <summary>Uses two independent browser contexts sharing the authenticated cookie to produce a real stale version, then replaces and synchronously clears output.</summary>
    internal static async Task<ApplicationEditorRegistration> RunAsync(IPage page, Uri origin, Uri issuer, DateTimeOffset now, Func<IPage, Task>? presentation = null, string? callback = null)
    {
        using var submissions = new ApplicationSubmission(page, Post, new Uri(origin, CollectionPath).AbsoluteUri);
        var result = await RunCoreAsync(page, origin, issuer, now, presentation, callback);
        submissions.RequireOnce();
        return result;
    }

    /// <summary>Retains actual creation, conflict and replacement UI while proving the original displayed credential before it is superseded.</summary>
    private static async Task<ApplicationEditorRegistration> RunCoreAsync(IPage page, Uri origin, Uri issuer, DateTimeOffset now, Func<IPage, Task>? presentation, string? callback)
    {
        var created = await CreateAsync(page, origin, now, presentation);
        var output = created.Secret;
        string id;
        await Button(page, Hide).ClickAsync();
        await HeadingAsync(page, DetailsTitle);
        await BrowserSecretOracle.StorageAsync(page, [output]);
        await using var concurrent = await ConcurrentAsync(page);
        var other = await concurrent.NewPageAsync();
        try
        {
            await OpenAsync(other, origin);
            await Button(other, EditAction).ClickAsync();
            await HeadingAsync(other, EditAction);
            id = await EditAsync(page, origin, created.Version, presentation, callback ?? UpdatedCallback);
            await other.Locator(DisplayField).FillAsync(StaleDraft);
            await Button(other, SaveAction).ClickAsync();
            await HeadingAsync(other, ProblemTitle);
            Assert.True(await other.GetByText(StaleMessage, new()
            {
                Exact = false
            }).IsVisibleAsync());
            await Button(other, ReloadAction).ClickAsync();
            await HeadingAsync(other, DetailsTitle);
            Assert.True(await other.GetByText(Updated, new()
            {
                Exact = true
            }).IsVisibleAsync());
            await BrowserSecretOracle.StorageAsync(other, [output]);
        }
        finally { await other.CloseAsync(); }
        await ApplicationCredentialEvidence.RequireAsync(issuer, Client, output, callback ?? UpdatedCallback);
        var replacement = await ReplaceAsync(page, now, output, presentation);
        return new(id, Client, callback ?? UpdatedCallback, replacement, output);
    }

    /// <summary>Copies only the existing authenticated cookie jar to an independent context, preserving visibility-driven clearing in the original tab.</summary>
    internal static async Task<IBrowserContext> ConcurrentAsync(IPage page)
    {
        var concurrent = await page.Context.Browser!.NewContextAsync();
        try
        {
            await concurrent.AddCookiesAsync((await page.Context.CookiesAsync()).Select(cookie => new Cookie
            {
                Name = cookie.Name,
                Value = cookie.Value,
                Domain = cookie.Domain,
                Path = cookie.Path,
                Expires = cookie.Expires,
                HttpOnly = cookie.HttpOnly,
                Secure = cookie.Secure,
                SameSite = cookie.SameSite
            }));
            return concurrent;
        }
        catch { await concurrent.DisposeAsync(); throw; }
    }

    /// <summary>Requires local linked validation and a genuinely generated one-time secret without recording its value in diagnostics.</summary>
    private static async Task<(string Secret, string Version)> CreateAsync(IPage page, Uri origin, DateTimeOffset now, Func<IPage, Task>? presentation = null)
    {
        await page.GotoAsync(new Uri(origin, Applications).AbsoluteUri);
        await Button(page, CreateAction).ClickAsync();
        await HeadingAsync(page, CreateAction);
        await PresentationAsync(page, presentation);
        await Button(page, CreateAction).ClickAsync();
        await page.GetByRole(AriaRole.Alert).WaitForAsync();
        Assert.True(await page.GetByRole(AriaRole.Alert).EvaluateAsync<bool>(SummaryFocus));
        await page.Locator(ClientField).FillAsync(Client);
        await page.Locator(DisplayField).FillAsync(Display);
        await page.Locator(CallbacksField).FillAsync(Callback);
        await page.Locator(ExpiryField).FillAsync(now.AddMinutes(LifetimeMinutes).ToString(ExpiryFormat, CultureInfo.InvariantCulture));
        await Button(page, CreateAction).FocusAsync();
        var response = await page.RunAndWaitForResponseAsync(() => page.Keyboard.PressAsync(EnterKey),
            item => item.Request.Method == Post && item.Url == new Uri(origin, CollectionPath).AbsoluteUri);
        Assert.Equal(CreatedStatus, response.Status);
        var version = await ApplicationEditorBinding.HeadersAsync(response);
        await HeadingAsync(page, CreatedTitle);
        var output = await page.Locator(Secret).InputValueAsync();
        ApplicationEditorBinding.Secret(output);
        return (output, version);
    }

    /// <summary>Opens the exact newly registered client through its actual protected list projection.</summary>
    private static async Task OpenAsync(IPage page, Uri origin)
    {
        await page.GotoAsync(new Uri(origin, Applications).AbsoluteUri);
        await Button(page, DetailsPrefix + Client).ClickAsync();
        await HeadingAsync(page, DetailsTitle);
    }

    /// <summary>Edits only mutable fields using the keyboard and verifies actual saved safe metadata.</summary>
    private static async Task<string> EditAsync(IPage page, Uri origin, string createdVersion, Func<IPage, Task>? presentation, string callback)
    {
        await Button(page, EditAction).ClickAsync();
        await HeadingAsync(page, EditAction);
        await PresentationAsync(page, presentation);
        Assert.Equal(0, await page.Locator(ClientField).CountAsync());
        await page.Locator(DisplayField).FillAsync(Updated);
        await page.Locator(CallbacksField).FillAsync(callback);
        await Button(page, SaveAction).FocusAsync();
        var response = await page.RunAndWaitForResponseAsync(() => page.Keyboard.PressAsync(EnterKey),
            item => item.Request.Method == Put && item.Url.StartsWith(new Uri(origin, CollectionPath).AbsoluteUri + ItemSeparator, StringComparison.Ordinal));
        Assert.Equal(UpdatedStatus, response.Status);
        var id = ApplicationEditorBinding.CreatedIdentity(origin, response.Url, createdVersion,
            await response.Request.HeaderValueAsync(Precondition), await ApplicationEditorBinding.HeadersAsync(response));
        await HeadingAsync(page, UpdatedTitle);
        Assert.True(await page.GetByText(callback, new()
        {
            Exact = true
        }).IsVisibleAsync());
        return id;
    }

    /// <summary>Requires unchecked confirmation, independent output and synchronous pagehide erasure, then verifies no browser persistence.</summary>
    private static async Task<string> ReplaceAsync(IPage page, DateTimeOffset now, string previous, Func<IPage, Task>? presentation)
    {
        await Button(page, ReplaceAction).ClickAsync();
        await HeadingAsync(page, ReplaceAction);
        await PresentationAsync(page, presentation);
        Assert.False(await page.Locator(ConfirmationField).IsCheckedAsync());
        await page.Locator(ExpiryField).FillAsync(now.AddMinutes(LifetimeMinutes + 1).ToString(ExpiryFormat, CultureInfo.InvariantCulture));
        await Button(page, ReplaceAction).ClickAsync();
        await page.GetByRole(AriaRole.Alert).WaitForAsync();
        await page.GetByRole(AriaRole.Alert).GetByRole(AriaRole.Link).Filter(new()
        {
            HasText = ConfirmationLabel
        }).FocusAsync();
        await page.Keyboard.PressAsync(EnterKey);
        Assert.True(await page.Locator(ConfirmationField).EvaluateAsync<bool>(SummaryFocus));
        await page.Keyboard.PressAsync(SpaceKey);
        await Button(page, ReplaceAction).ClickAsync();
        await HeadingAsync(page, ReplacedTitle);
        var output = await page.Locator(Secret).InputValueAsync();
        Assert.True(output.Length == SecretLength && output != previous);
        await page.EvaluateAsync(HidePage);
        Assert.Equal(0, await page.Locator(Secret).CountAsync());
        await HeadingAsync(page, ProblemTitle);
        await BrowserSecretOracle.StorageAsync(page, [previous, output]);
        await PresentationAsync(page, presentation);
        return output;
    }

    /// <summary>Checks forms and safe failure states in both themes and each required width without recording authenticated pixels.</summary>
    private static async Task PresentationAsync(IPage page, Func<IPage, Task>? presentation)
    {
        foreach (var theme in new[] { LightTheme, DarkTheme })
        {
            await page.GetByRole(AriaRole.Combobox, new()
            {
                Name = Appearance
            }).SelectOptionAsync(theme);
            if (presentation is not null)
            {
                await presentation(page);
            }
        }
    }

    /// <summary>Locates one explicit visible user action without leaking protocol data.</summary>
    private static ILocator Button(IPage page, string name) => page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
    /// <summary>Checks the finite visible result and focus after each transition; secret values never enter status announcements.</summary>
    private static async Task HeadingAsync(IPage page, string title)
    {
        await Assertions.Expect(page.Locator(Heading)).ToHaveTextAsync(title);
        Assert.True(await page.EvaluateAsync<bool>(Focus));
    }
}
