using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Requires real history and visibility transitions to erase actual API-created one-time output.</summary>
internal static class ApplicationOutputLifecycle
{
    private const string Visible = "() => document.visibilityState === 'visible'";
    private const string Hidden = "() => document.visibilityState === 'hidden'";
    private const string Root = "/";
    private const int PollingMilliseconds = 50;
    private const int VisibilityMilliseconds = 3000;

    /// <summary>Uses a separate genuine account/provider fixture for each native lifecycle boundary and keeps actual secrets private.</summary>
    internal static async Task<string[]> RunAsync(BootstrapFixture fixture, string provider, bool history)
    {
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true);
        await using var browser = await BrowserSession.CreateHeadedAsync();
        var page = await browser.Context.NewPageAsync();
        var access = await ApplicationAccessFixture.SignInAsync(environment, page);
        var visibility = await ApplicationNativeVisibility.CreateAsync(page);
        await page.Clock.SetFixedTimeAsync(environment.Issuer.Account.Journey.Clock.Now.UtcDateTime);
        await using var observation = await ApplicationLifecycleObservation.CreateAsync(page);
        await ApplicationLifecycleUi.PrepareAsync(page, environment.Profile.ClientUri, environment.Issuer.Account.Journey.Clock.Now, ApplicationLifecycleUi.LongLifetimeSeconds);
        var secret = await ApplicationLifecycleUi.CreateAsync(page);
        if (history)
        {
            await HistoryAsync(page, environment.Profile.ClientUri, observation);
            await page.GoForwardAsync();
            await page.GoBackAsync();
            Assert.True(page.Url == new Uri(environment.Profile.ClientUri, ApplicationLifecycleUi.Applications).AbsoluteUri);
        }
        else
        {
            await HideAsync(page, visibility, observation);
        }

        Assert.Equal(0, await page.Locator(ApplicationLifecycleUi.Secret).CountAsync());
        await BrowserSecretOracle.StorageAsync(page, [secret, access.Value]);
        await observation.RequireAsync([secret, access.Value]);
        return [secret, access.Value];
    }

    /// <summary>Navigates away and returns using native history; persisted pageshow is recorded but never manufactured or required.</summary>
    internal static async Task HistoryAsync(IPage page, Uri origin, ApplicationLifecycleObservation observation)
    {
        var hides = observation.Hides;
        var shows = observation.Shows;
        await page.GotoAsync(new Uri(origin, Root).AbsoluteUri);
        await page.GoBackAsync();
        Assert.True(page.Url == new Uri(origin, ApplicationLifecycleUi.Applications).AbsoluteUri);
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = ApplicationLifecycleUi.Create,
            Exact = true
        }).Or(page.Locator(ApplicationLifecycleUi.Heading)).First.WaitForAsync();
        await observation.RequireTransitionsAsync(previousHides: hides, previousShows: shows);
        Assert.Equal(0, await page.Locator(ApplicationLifecycleUi.Secret).CountAsync());
    }

    /// <summary>Minimizes and restores the owned native window and requires real hidden/visible state; no synthetic event can satisfy the oracle.</summary>
    internal static async Task HideAsync(IPage page, ApplicationNativeVisibility visibility, ApplicationLifecycleObservation observation)
    {
        await visibility.RestoreAsync();
        await page.WaitForFunctionAsync(Visible, options: new()
        {
            Timeout = VisibilityMilliseconds,
            PollingInterval = PollingMilliseconds
        });
        var hidden = observation.Hidden;
        await visibility.MinimizeAsync();
        await page.WaitForFunctionAsync(Hidden, options: new()
        {
            Timeout = VisibilityMilliseconds,
            PollingInterval = PollingMilliseconds
        });
        await observation.RequireTransitionsAsync(previousHidden: hidden);
        Assert.Equal(0, await page.Locator(ApplicationLifecycleUi.Secret).CountAsync());
        await visibility.RestoreAsync();
        await page.WaitForFunctionAsync(Visible, options: new()
        {
            Timeout = VisibilityMilliseconds,
            PollingInterval = PollingMilliseconds
        });
        await ApplicationLifecycleUi.HeadingAsync(page, ApplicationLifecycleUi.Problem);
    }
}
