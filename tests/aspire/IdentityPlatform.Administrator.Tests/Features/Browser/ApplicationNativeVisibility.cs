using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Removes Chromium's automation-only focus override inside a disposable headed lifecycle fixture; never synthesizes visibility.</summary>
internal sealed class ApplicationNativeVisibility : IAsyncDisposable
{
    private const string Chromium = "chromium";
    private const string WebKit = "webkit";
    private const string Enabled = "enabled";
    private const string FocusOverride = "Emulation.setFocusEmulationEnabled";
    private const string Hidden = "() => document.visibilityState === 'hidden'";
    private const string Visible = "() => document.visibilityState === 'visible'";
    private const int PollingMilliseconds = 50;
    private const int ObservationMilliseconds = 3000;
    private readonly List<ICDPSession> sessions = [];
    private ApplicationNativeWindow window = null!;

    /// <summary>Disables Chromium's public focus override; the lifecycle wrapper separately restores WebKit's native activity state.</summary>
    internal static async Task<ApplicationNativeVisibility> CreateAsync(IPage page)
    {
        var owner = new ApplicationNativeVisibility();
        try
        {
            Assert.True(BrowserSession.Engine is Chromium or WebKit);
            if (BrowserSession.Engine == Chromium)
            {
                foreach (var target in new[] { page })
                {
                    var session = await target.Context.NewCDPSessionAsync(target);
                    owner.sessions.Add(session);
                    await session.SendAsync(FocusOverride, new Dictionary<string, object>
                    {
                        [Enabled] = false
                    });
                }
            }
            owner.window = await ApplicationNativeWindow.FindAsync(page);
            await owner.RequireNativeCycleAsync(page);
            return owner;
        }
        catch { await owner.DisposeAsync(); throw; }
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

    /// <summary>Joins owned public CDP detach operations and retains all cleanup failures before the owning browser is disposed.</summary>
    public async ValueTask DisposeAsync()
    {
        List<Exception> failures = [];
        foreach (var session in sessions)
        {
            try
            {
                await session.DetachAsync();
            }
            catch (Exception error) { failures.Add(error); }
        }
        sessions.Clear();
        if (failures.Count != 0)
        {
            throw new AggregateException(failures);
        }
    }
}
