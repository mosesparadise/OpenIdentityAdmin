using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Requires native window visibility after the isolated lifecycle driver has disabled its own automation override.</summary>
internal sealed class ApplicationNativeVisibility
{
    private const string Chromium = "chromium";
    private const string WebKit = "webkit";
    private const string Hidden = "() => document.visibilityState === 'hidden'";
    private const string Visible = "() => document.visibilityState === 'visible'";
    private const int PollingMilliseconds = 50;
    private const int ObservationMilliseconds = 3000;
    private readonly ApplicationNativeWindow window;

    /// <summary>The outer browser session owns the native window and its lifetime.</summary>
    private ApplicationNativeVisibility(ApplicationNativeWindow window) => this.window = window;

    /// <summary>The lifecycle wrapper disables each engine's own driver-session override before this real window is acquired.</summary>
    internal static async Task<ApplicationNativeVisibility> CreateAsync(IPage page)
    {
        Assert.True(BrowserSession.Engine is Chromium or WebKit);
        var owner = new ApplicationNativeVisibility(await ApplicationNativeWindow.FindAsync(page));
        await owner.RequireNativeCycleAsync(page);
        return owner;
    }

    /// <summary>Rejects an environment that cannot produce real hidden/visible transitions before any secret or pending-write assertion is attempted.</summary>
    private async Task RequireNativeCycleAsync(IPage page)
    {
        await RestoreAsync();
        await page.WaitForFunctionAsync(Visible, options: new()
        {
            Timeout = ObservationMilliseconds,
            PollingInterval = PollingMilliseconds
        });
        await MinimizeAsync();
        await page.WaitForFunctionAsync(Hidden, options: new()
        {
            Timeout = ObservationMilliseconds,
            PollingInterval = PollingMilliseconds
        });
        await RestoreAsync();
        await page.WaitForFunctionAsync(Visible, options: new()
        {
            Timeout = ObservationMilliseconds,
            PollingInterval = PollingMilliseconds
        });
    }

    /// <summary>Minimizes the exact native browser window on the isolated desktop.</summary>
    internal Task MinimizeAsync() => window.MinimizeAsync();

    /// <summary>Restores the same native window before any further user interaction.</summary>
    internal Task RestoreAsync() => window.RestoreAsync();

}
