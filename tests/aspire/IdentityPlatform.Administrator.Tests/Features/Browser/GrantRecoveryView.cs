using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Exercises shipped confirmation, recovery, focus and retained-theme presentation through explicit browser actions.</summary>
internal static class GrantRecoveryView
{
    internal const string Continue = "Continue stopped revocation";
    internal const string Revoke = "Revoke grants";
    internal const string Recover = "Check acceptance of this request";
    internal const string Unknown = "Your request may already be accepted.";
    internal const string Stale = "Application or job details changed.";
    internal const string Forbidden = "Current grant permissions are required.";
    internal const string Completed = "Revocation completed. The application remains disabled until you explicitly re-enable it.";
    internal const int Accepted = 202;
    internal const int Denied = 403;
    internal const int Conflict = 412;
    private const string Region = "section[aria-labelledby='grant-heading']";
    private const string Heading = "#grant-heading";
    private const string Confirmation = "#grant-confirm";
    private const string Reason = "#grant-reason";
    private const string Security = "security";
    private const string Cancel = "Cancel";
    private const string Error = "#grant-error";
    private const string Appearance = "Appearance";
    private static readonly string[] Themes = ["light", "dark"];
    private const string Enter = "Enter";
    private const string SubmitNew = "Confirm revocation";
    private const string SubmitContinue = "Confirm continuation";
    private const string Applications = "/applications";
    private const string Details = "View details for ";
    private const string Reference = "Job reference: ";
    private const string Status = "Revocation status:";
    private const string Stopped = "Revocation status: stopped";
    private const string Counts = "Authorizations revoked: 1. Tokens revoked: 2.";
    private const string CountPrefix = "Authorizations revoked:";
    private const string AuthorityStop = "Work stopped (authority). Earlier committed progress is preserved.";

    /// <summary>Loads current application details through the real list and details endpoints.</summary>
    internal static async Task OpenAsync(IPage page, Uri origin, ApplicationEditorRegistration registration)
    {
        await page.GotoAsync(new Uri(origin, Applications).AbsoluteUri);
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = Details + registration.ClientId,
            Exact = true
        }).ClickAsync();
    }

    /// <summary>Scopes every grant action away from unrelated application editor controls.</summary>
    internal static ILocator Button(IPage page, string name) => page.Locator(Region).GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    /// <summary>Opens an unchecked form, tests required confirmation/cancel focus when requested, then prepares one finite security intent.</summary>
    internal static async Task PrepareAsync(IPage page, bool continuation, bool validation = false)
    {
        var action = continuation ? Continue : Revoke;
        await Button(page, action).ClickAsync();
        await Assertions.Expect(page.Locator(Heading)).ToBeFocusedAsync();
        Assert.False(await page.Locator(Confirmation).IsCheckedAsync());
        if (validation)
        {
            await Button(page, continuation ? SubmitContinue : SubmitNew).ClickAsync();
            await page.Locator(Error).WaitForAsync();
            await Button(page, Cancel).ClickAsync();
            await Assertions.Expect(Button(page, action)).ToBeFocusedAsync();
            await Button(page, action).ClickAsync();
            Assert.False(await page.Locator(Confirmation).IsCheckedAsync());
        }
        await page.Locator(Reason).SelectOptionAsync(Security);
        await page.Locator(Confirmation).CheckAsync();
    }

    /// <summary>Submits by keyboard and observes only the actual matching HTTP response, without replacing its content.</summary>
    internal static Task<IResponse> SubmitAsync(IPage page, string url, bool continuation) => page.RunAndWaitForResponseAsync(
        () => Button(page, continuation ? SubmitContinue : SubmitNew).PressAsync(Enter), value => value.Url == url && value.Request.Method == GrantRecoveryTraffic.Post);

    /// <summary>Submits a request whose genuinely committed response will be lost at the explicit browser seam.</summary>
    internal static Task SendLostAsync(IPage page) => Button(page, SubmitNew).PressAsync(Enter);

    /// <summary>Requires visible finite messaging and native focus rather than inferring state from a controller test.</summary>
    internal static async Task MessageAsync(IPage page, string text)
    {
        await Assertions.Expect(page.Locator(Region).GetByText(text, new()
        {
            Exact = false
        })).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator(Heading)).ToBeFocusedAsync();
    }

    /// <summary>Requires the real stopped job and its committed partial counts to be visible before opening a continuation form.</summary>
    internal static async Task StoppedAsync(IPage page, Guid job)
    {
        var region = page.Locator(Region);
        await Assertions.Expect(region.GetByText(Reference + job, new()
        {
            Exact = true
        })).ToBeVisibleAsync();
        await Assertions.Expect(region.GetByText(Stopped, new()
        {
            Exact = true
        })).ToBeVisibleAsync();
        await Assertions.Expect(region.GetByText(Counts, new()
        {
            Exact = true
        })).ToBeVisibleAsync();
        await Assertions.Expect(region.GetByText(AuthorityStop, new()
        {
            Exact = false
        })).ToBeVisibleAsync();
    }

    /// <summary>Permission denial must hide protected progress and fresh mutation controls while explicit same-intent recovery remains available.</summary>
    internal static async Task HiddenProgressAsync(IPage page)
    {
        var region = page.Locator(Region);
        foreach (var prefix in new[] { Reference, Status, CountPrefix })
        {
            await Assertions.Expect(region.GetByText(prefix, new()
            {
                Exact = false
            })).ToHaveCountAsync(0);
        }
        await Assertions.Expect(Button(page, Revoke)).ToHaveCountAsync(0);
        await Assertions.Expect(Button(page, Continue)).ToHaveCountAsync(0);
        await Assertions.Expect(Button(page, Recover)).ToBeVisibleAsync();
    }

    /// <summary>Checks exact canonical request bindings, separating creation from same-job continuation.</summary>
    internal static void Require(GrantRecoveryIntent intent, string url, string applicationVersion, string? jobVersion = null)
    {
        Assert.True(intent.Valid);
        Assert.Equal(GrantRecoveryIntent.Digest(url), intent.Url);
        Assert.Equal(GrantRecoveryIntent.Digest(applicationVersion), intent.Version);
        Assert.Equal(jobVersion is null ? null : GrantRecoveryIntent.Digest(jobVersion), intent.JobVersion);
    }

    /// <summary>Runs existing reflow/keyboard/axe checks for actual recovery states in both retained themes.</summary>
    internal static async Task PresentationAsync(IPage page)
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
}
