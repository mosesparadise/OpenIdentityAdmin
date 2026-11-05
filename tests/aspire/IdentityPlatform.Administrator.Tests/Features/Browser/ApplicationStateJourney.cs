using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Shares actual application disable/re-enable UI and independent native issuer admission between retained browsers and ordinary Aspire demos.</summary>
internal static class ApplicationStateJourney
{
    private const string Appearance = "Appearance";
    private const string EditorHeading = "#editor-heading";
    private static readonly string[] Themes = ["light", "dark"];
    private const string Applications = "/applications";
    private const string Collection = "/bff/api/v1/applications/";
    private const string Authorize = "/connect/authorize";
    private const string DetailsPrefix = "View details for ";
    private const string DetailsTitle = "Application details";
    private const string UpdatedTitle = "Application updated";
    private const string Disable = "Disable application";
    private const string Enable = "Re-enable application";
    private const string DisableSuffix = "/disable";
    private const string EnableSuffix = "/enable";
    private const string Cancel = "Cancel";
    private const string Reason = "#lifecycle-reason";
    private const string Confirm = "#lifecycle-confirm";
    private const string Maintenance = "maintenance";
    private const string Enter = "Enter";
    private const string Space = "Space";
    private const string Focused = "node => node === document.activeElement";
    private const string Post = "POST";
    private const string EnabledField = "enabled";
    private const string CredentialField = "credential";
    private const string SecretField = "clientSecret";
    private const string NoStore = "no-store";
    private const string CacheControl = "cache-control";
    private const string Password = "#password";
    private const int Success = 200;
    private const int Unavailable = 503;

    /// <summary>Exercises cancel focus, required confirmation, finite reason selection and keyboard submission against the shipped fixed route.</summary>
    internal static async Task ChangeAsync(IPage page, Uri origin, ApplicationEditorRegistration registration, bool enabled, Func<IPage, Task>? presentation = null)
    {
        await page.GotoAsync(new Uri(origin, Applications).AbsoluteUri);
        await Button(page, DetailsPrefix + registration.ClientId).ClickAsync();
        await HeadingAsync(page, DetailsTitle);
        var action = enabled ? Enable : Disable;
        await Button(page, action).ClickAsync();
        await HeadingAsync(page, action);
        await Button(page, Cancel).ClickAsync();
        await HeadingAsync(page, DetailsTitle);
        Assert.True(await Button(page, action).EvaluateAsync<bool>(Focused));
        await Button(page, action).ClickAsync();
        await HeadingAsync(page, action);
        await PresentationAsync(page, presentation);
        await Button(page, action).ClickAsync();
        await page.GetByRole(AriaRole.Alert).WaitForAsync();
        Assert.True(await page.GetByRole(AriaRole.Alert).EvaluateAsync<bool>(Focused));
        await PresentationAsync(page, presentation);
        await SubmitAsync(page, origin, registration, enabled, presentation);
    }

    /// <summary>Submits one confirmed keyboard action and verifies committed output before checking success focus and presentation.</summary>
    private static async Task SubmitAsync(IPage page, Uri origin, ApplicationEditorRegistration registration, bool enabled, Func<IPage, Task>? presentation)
    {
        var action = enabled ? Enable : Disable;
        await page.Locator(Reason).SelectOptionAsync(Maintenance);
        await page.Locator(Confirm).FocusAsync();
        await page.Keyboard.PressAsync(Space);
        await Button(page, action).FocusAsync();
        var url = new Uri(origin, Collection + Uri.EscapeDataString(registration.Id) + (enabled ? EnableSuffix : DisableSuffix)).AbsoluteUri;
        var response = await page.RunAndWaitForResponseAsync(() => page.Keyboard.PressAsync(Enter), item => item.Request.Method == Post && item.Url == url);
        Assert.Equal(Success, response.Status);
        using var body = await ApplicationEditorResponse.ReadAsync(page, response, new Uri(origin, Applications));
        Assert.Equal(enabled, body.RootElement.GetProperty(EnabledField).GetBoolean());
        Assert.False(body.RootElement.TryGetProperty(SecretField, out _));
        Assert.Equal(System.Text.Json.JsonValueKind.Object, body.RootElement.GetProperty(CredentialField).ValueKind);
        await HeadingAsync(page, UpdatedTitle);
        Assert.True(await page.Locator(EditorHeading).EvaluateAsync<bool>(Focused));
        await PresentationAsync(page, presentation);
        Assert.True(await Button(page, enabled ? Disable : Enable).IsVisibleAsync());
        await BrowserSecretOracle.StorageAsync(page, registration.Secrets);
    }

    /// <summary>Inspects each requested lifecycle state in both actual shell themes; callers without presentation retain their native demo scope.</summary>
    private static async Task PresentationAsync(IPage page, Func<IPage, Task>? presentation)
    {
        if (presentation is null)
        {
            return;
        }
        foreach (var theme in Themes)
        {
            await page.GetByRole(AriaRole.Combobox, new()
            {
                Name = Appearance
            }).SelectOptionAsync(theme);
            await presentation(page);
        }
    }

    /// <summary>A new independent PKCE/state/nonce request receives local native denial before a password form or relying-party callback can occur.</summary>
    internal static async Task RequireDeniedAsync(IPage page, Uri issuer, ApplicationEditorRegistration registration)
    {
        var input = ManagedAuthorizationRequest.Initial(ManagedAuthorizationRequest.Random(), registration.ClientId, registration.Callback);
        var location = QueryHelpers.AddQueryString(new Uri(issuer, Authorize).AbsoluteUri, input!);
        var response = await page.GotoAsync(location);
        Assert.NotNull(response);
        Assert.Equal(Unavailable, response.Status);
        Assert.Contains(NoStore, (await response.HeaderValueAsync(CacheControl))!);
        Assert.True(new Uri(page.Url).GetLeftPart(UriPartial.Authority) == issuer.GetLeftPart(UriPartial.Authority));
        Assert.Equal(0, await page.Locator(Password).CountAsync());
    }

    /// <summary>Finds a single named action without relying on presentation order or implementation class names.</summary>
    private static ILocator Button(IPage page, string name) => page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    /// <summary>Waits for a finite visible transition without logging protocol URLs or private inputs.</summary>
    private static Task HeadingAsync(IPage page, string name) => page.GetByRole(AriaRole.Heading, new() { Name = name, Exact = true }).WaitForAsync();
}
