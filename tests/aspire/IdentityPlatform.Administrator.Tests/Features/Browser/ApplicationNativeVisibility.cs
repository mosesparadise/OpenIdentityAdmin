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
    private const int ObservationMilliseconds = 3000;
    private readonly List<ICDPSession> sessions = [];

    /// <summary>Uses public CDP only to disable Playwright's Chromium override on both pages; WebKit retains its native activation implementation.</summary>
    internal static async Task<ApplicationNativeVisibility> CreateAsync(IPage page, IPage other)
    {
        var owner = new ApplicationNativeVisibility();
        try
        {
            Assert.True(BrowserSession.Engine is Chromium or WebKit);
            if (BrowserSession.Engine == Chromium)
            {
                foreach (var target in new[] { page, other })
                {
                    var session = await target.Context.NewCDPSessionAsync(target);
                    owner.sessions.Add(session);
                    await session.SendAsync(FocusOverride, new Dictionary<string, object>
                    {
                        [Enabled] = false
                    });
                }
            }
            await RequireNativeCycleAsync(page, other);
            return owner;
        }
        catch { await owner.DisposeAsync(); throw; }
    }

    /// <summary>Rejects an environment that cannot produce real hidden/visible transitions before any secret or pending-write assertion is attempted.</summary>
    private static async Task RequireNativeCycleAsync(IPage page, IPage other)
    {
        await page.BringToFrontAsync();
        await page.WaitForFunctionAsync(Visible, options: new()
        {
            Timeout = ObservationMilliseconds
        });
        await other.BringToFrontAsync();
        await page.WaitForFunctionAsync(Hidden, options: new()
        {
            Timeout = ObservationMilliseconds
        });
        await page.BringToFrontAsync();
        await page.WaitForFunctionAsync(Visible, options: new()
        {
            Timeout = ObservationMilliseconds
        });
    }

    /// <summary>Joins both public CDP detach operations and retains all cleanup failures before the owning browser is disposed.</summary>
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
