using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Checks each working message in a separately bounded request, never pausing one request across the full layout matrix.</summary>
internal static class EditorRecoveryWorking
{
    private const string Reading = "Loading application details…";
    private const string Creating = "Creating application…";
    private const string Editing = "Saving changes…";
    private const string Replacing = "Replacing secret…";
    private const string Read = "read";
    private const string Create = "create";
    private const string Edit = "edit";
    private const string Replace = "replace";
    private const string Get = "GET";
    private const string Stop = "Stop waiting";
    private const string Cleared = "The editor was cleared when you left or hid this view.";
    private const string Layout = "() => document.documentElement.scrollWidth <= innerWidth";
    private static readonly string[] Operations = [Read, Create, Edit, Replace];
    private static readonly TimeSpan ScanBudget = TimeSpan.FromSeconds(5);

    /// <summary>Each request has its own unchanged ten-second transport bound and one short accessibility scan.</summary>
    internal static async Task RequireAsync(BrowserHttpsFixture environment, IPage page, string id)
    {
        var before = await EditorRecoveryStore.ReadAsync(environment, id);
        foreach (var operation in Operations)
        {
            foreach (var theme in EditorRecoveryView.Themes)
            {
                foreach (var width in EditorRecoveryView.Widths)
                {
                    await PrepareAsync(environment, page, operation);
                    await EditorRecoveryView.ThemeAsync(page, theme);
                    await page.SetViewportSizeAsync(width, EditorRecoveryView.Height);
                    await InspectAsync(environment, page, id, operation);
                }
            }
        }
        Assert.Equal(before, await EditorRecoveryStore.ReadAsync(environment, id));
        await EditorRecoveryView.PresentationAsync(page);
    }

    /// <summary>Gates a read or CSRF acquisition, so no deliberately stopped working-state check can reach mutation dispatch.</summary>
    private static async Task InspectAsync(BrowserHttpsFixture environment, IPage page, string id, string operation)
    {
        var address = operation == Read ? EditorRecoveryFailures.Address(environment, id)
            : new Uri(environment.Profile.ClientUri, EditorRecoveryView.Token).AbsoluteUri;
        using var sends = new EditorRecoverySends(page);
        await using var probe = await EditorRecoveryResponse.AttachAsync(page, address, Get, EditorRecoveryResponse.Behavior.HoldWithoutDispatch);
        await EditorRecoveryView.Button(page, Action(operation)).ClickAsync();
        await probe.EnteredAsync();
        await EditorRecoveryView.HeadingAsync(page, Title(operation));
        try
        {
            Assert.True(await page.EvaluateAsync<bool>(Layout));
            await BrowserAccessibility.RequireNoViolationsAsync(page).WaitAsync(ScanBudget, TestContext.Current.CancellationToken);
        }
        finally { await EditorRecoveryView.Button(page, Stop).ClickAsync(); }
        await EditorRecoveryView.HeadingAsync(page, EditorRecoveryView.Problem);
        Assert.True(await page.GetByText(Cleared, new()
        {
            Exact = false
        }).IsVisibleAsync());
        Assert.Equal(1, probe.Sends);
        Assert.Equal(0, sends.Count);
        Assert.Equal(0, await page.Locator(EditorRecoveryView.Secret).CountAsync());
    }

    /// <summary>Opens each actual operation with valid explicit input before its proof/read request is gated.</summary>
    private static async Task PrepareAsync(BrowserHttpsFixture environment, IPage page, string operation)
    {
        if (operation is Read or Create)
        {
            await page.GotoAsync(new Uri(environment.Profile.ClientUri, EditorRecoveryView.Applications).AbsoluteUri);
            if (operation == Read)
            {
                return;
            }
            await EditorRecoveryView.Button(page, EditorRecoveryView.Create).ClickAsync();
            await EditorRecoveryView.FillCreateAsync(page, environment.Issuer.Account.Journey.Clock.Now);
            return;
        }
        await EditorRecoveryView.OpenAsync(page, environment.Profile.ClientUri);
        await EditorRecoveryView.Button(page, operation == Edit ? EditorRecoveryView.Edit : EditorRecoveryView.Replace).ClickAsync();
        if (operation == Replace)
        {
            await EditorRecoveryView.FillExpiryAsync(page, environment.Issuer.Account.Journey.Clock.Now);
            await page.Locator(EditorRecoveryView.Confirmation).CheckAsync();
        }
    }

    /// <summary>Selects finite shipped button names; no response text or application input contributes diagnostics.</summary>
    private static string Action(string operation) => operation switch
    {
        Read => EditorRecoveryView.ViewPrefix + EditorRecoveryView.Client,
        Create => EditorRecoveryView.Create,
        Edit => EditorRecoveryView.Save,
        _ => EditorRecoveryView.Replace
    };

    /// <summary>Each controller working branch has its exact visible heading checked.</summary>
    private static string Title(string operation) => operation switch
    {
        Read => Reading,
        Create => Creating,
        Edit => Editing,
        _ => Replacing
    };
}
