using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Shares real confirmation, bounded progress and completion UI between retained browsers and the ordinary Aspire Worker demo.</summary>
internal static class ApplicationGrantJourney
{
    private const string Applications = "/applications";
    private const string Collection = "/bff/api/v1/applications/";
    private const string Suffix = "/grant-revocations";
    private const string Panel = "section[aria-labelledby='grant-heading']";
    private const string Heading = "#grant-heading";
    private const string Reference = ".application-client-id";
    private const string Reason = "#grant-reason";
    private const string Confirmation = "#grant-confirm";
    private const string Error = "#grant-error";
    private const string Revoke = "Revoke grants";
    private const string Submit = "Confirm revocation";
    private const string Cancel = "Cancel";
    private const string Details = "View details for ";
    private const string Post = "POST";
    private const string Location = "location";
    private const string Key = "idempotency-key";
    private const string Precondition = "if-match";
    private const string GuidFormat = "D";
    private const string Completed = "Revocation completed. The application remains disabled until you explicitly re-enable it.";
    private const string Counts = "Authorizations revoked: 1. Tokens revoked: 2.";
    private const string Appearance = "Appearance";
    private static readonly string[] Themes = ["light", "dark"];
    private const int Accepted = 202;
    private const string Maintenance = "maintenance";
    private const string Enter = "Enter";
    private const string Separator = "/";
    private const int DisabledRevision = 4;
    private const int CompletionMilliseconds = 30000;

    /// <summary>Uses real browser requests and UI-only response observation; no browser inspector body read or synthetic response supplies acceptance.</summary>
    internal static async Task<string> RunAsync(IPage page, Uri origin, ApplicationEditorRegistration registration, Func<IPage, Task>? presentation = null)
    {
        await page.GotoAsync(new Uri(origin, Applications).AbsoluteUri);
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = Details + registration.ClientId,
            Exact = true
        }).ClickAsync();
        var panel = page.Locator(Panel);
        await Button(panel, Revoke).ClickAsync();
        await Assertions.Expect(page.Locator(Heading)).ToBeFocusedAsync();
        await Button(panel, Submit).ClickAsync();
        await page.Locator(Error).WaitForAsync();
        await Button(panel, Cancel).ClickAsync();
        await Assertions.Expect(Button(panel, Revoke)).ToBeFocusedAsync();
        Assert.Equal(0, await page.Locator(Error).CountAsync());
        await Button(panel, Revoke).ClickAsync();
        await PresentationAsync(page, presentation);
        await page.Locator(Reason).SelectOptionAsync(Maintenance);
        await page.Locator(Confirmation).CheckAsync();
        var path = Collection + Uri.EscapeDataString(registration.Id) + Suffix;
        var target = new Uri(origin, path).AbsoluteUri;
        using var submissions = new ApplicationSubmission(page, Post, target);
        var response = await page.RunAndWaitForResponseAsync(() => Button(panel, Submit).PressAsync(Enter), value => value.Url == target && value.Request.Method == Post);
        var id = await AcceptanceAsync(response, path, registration.Id);
        await Assertions.Expect(panel.Locator(Reference)).ToHaveTextAsync(id);
        await Assertions.Expect(page.Locator(Heading)).ToBeFocusedAsync();
        await Assertions.Expect(panel.GetByText(Completed, new()
        {
            Exact = true
        })).ToBeVisibleAsync(new()
        {
            Timeout = CompletionMilliseconds
        });
        await Assertions.Expect(panel.GetByText(Counts, new()
        {
            Exact = true
        })).ToBeVisibleAsync();
        await PresentationAsync(page, presentation);
        await BrowserSecretOracle.StorageAsync(page, registration.Secrets);
        submissions.RequireOnce();
        return id;
    }

    /// <summary>Binds the accepted opaque resource to canonical key, original disabled version and no-store security headers.</summary>
    private static async Task<string> AcceptanceAsync(IResponse response, string path, string applicationId)
    {
        Assert.Equal(Accepted, response.Status);
        var location = await response.HeaderValueAsync(Location);
        Assert.True(location?.StartsWith(path + Separator, StringComparison.Ordinal) == true);
        var id = location![(path.Length + 1)..];
        Assert.True(Guid.TryParseExact(id, GuidFormat, out var parsed) && parsed != Guid.Empty && parsed.ToString(GuidFormat) == id);
        var key = await response.Request.HeaderValueAsync(Key);
        Assert.True(Guid.TryParseExact(key, GuidFormat, out var request) && request != Guid.Empty && request.ToString(GuidFormat) == key);
        Assert.True(await response.Request.HeaderValueAsync(Precondition) == ApplicationEditorBinding.Version(applicationId, DisabledRevision));
        Assert.True(await ApplicationEditorBinding.HeadersAsync(response) == ApplicationEditorBinding.Version(id, 1));
        return id;
    }

    /// <summary>Checks the actual grant form and completion in both existing themes without storing private browser artifacts.</summary>
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

    /// <summary>Scopes action names to the grant region so application editor actions cannot satisfy the journey.</summary>
    private static ILocator Button(ILocator panel, string name) => panel.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
}
