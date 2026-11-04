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
    private const int VisibilityMilliseconds = 3000;

    /// <summary>Uses a separate genuine account/provider fixture for each native lifecycle boundary and keeps actual secrets private.</summary>
    internal static async Task<string[]> RunAsync(BootstrapFixture fixture, string provider, bool history)
    {
        await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true);
        await using var browser = await BrowserSession.CreateHeadedAsync();
        var other = await browser.Context.NewPageAsync();
        var page = await browser.Context.NewPageAsync();
        var access = await ApplicationAccessFixture.SignInAsync(environment, page);
        await using var visibility = await ApplicationNativeVisibility.CreateAsync(page, other);
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
            await HideAsync(page, other, observation);
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

    /// <summary>Uses native tab activation and requires observed hidden/visible state; engines unable to expose it do not receive a synthetic pass.</summary>
    internal static async Task HideAsync(IPage page, IPage other, ApplicationLifecycleObservation observation)
    {
        await page.BringToFrontAsync();
        await page.WaitForFunctionAsync(Visible, options: new()
        {
            Timeout = VisibilityMilliseconds
        });
        var hidden = observation.Hidden;
        await other.BringToFrontAsync();
        await page.WaitForFunctionAsync(Hidden, options: new()
        {
            Timeout = VisibilityMilliseconds
        });
        await observation.RequireTransitionsAsync(previousHidden: hidden);
        Assert.Equal(0, await page.Locator(ApplicationLifecycleUi.Secret).CountAsync());
        await page.BringToFrontAsync();
        await page.WaitForFunctionAsync(Visible, options: new()
        {
            Timeout = VisibilityMilliseconds
        });
        await ApplicationLifecycleUi.HeadingAsync(page, ApplicationLifecycleUi.Problem);
    }
}
