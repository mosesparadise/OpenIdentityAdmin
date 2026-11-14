using Microsoft.Playwright;

namespace IdentityPlatform.AppHost.Tests.Features.Samples;

/// <summary>Owns a full ordinary Aspire sample walkthrough and fresh disposable run for each retained provider/browser cell.</summary>
internal static class SampleRuntimeScenario
{
    private const int DeadlineMinutes = 9;
    private const string Startup = "sample process readiness";
    private const string Setup = "administrator catalogue and protected sample setup";
    private const string Authentication = "sample password, MFA and consent";
    private const string Resource = "sample profile and API";
    private const string Isolation = "sample browser isolation";
    private const string Restart = "fresh disposable sample run";
    private const string Waiting = "Configure this sample";
    private const string SignedIn = "Signed in as";
    private const string FailurePrefix = "Sample runtime verification failed during ";
    private const string FailureSuffix = ". Private browser, credential and protocol output was not retained.";

    /// <summary>Executes UI-created policy and dashboard stdin before native client/resource calls; existing administrator journeys remain separate.</summary>
    internal static async Task RunAsync(string provider, string engine)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(DeadlineMinutes));
        var phase = Startup;
        try
        {
            var cookies = await JourneyAsync(provider, engine, value => phase = value, deadline.Token);
            phase = Restart;
            await using var fresh = await LocalDemoRuntime.StartAsync(provider, deadline.Token);
            await fresh.WaitForSamplesAsync(deadline.Token);
            await using var replay = await LocalDemoBrowser.CreateAsync(engine);
            await replay.RestoreAsync(cookies);
            await replay.Page.GotoAsync(SampleBrowserJourney.Address);
            await replay.Page.GetByText(Waiting, new()
            {
                Exact = true
            }).WaitForAsync();
            Assert.Equal(0, await replay.Page.GetByText(SignedIn, new()
            {
                Exact = false
            }).CountAsync());
        }
        catch (Exception)
        {
            throw new InvalidOperationException(FailurePrefix + phase + FailureSuffix);
        }
    }

    /// <summary>Retains only real protected cookies in memory for restart verification after all resources dispose normally.</summary>
    private static async Task<Cookie[]> JourneyAsync(string provider, string engine, Action<string> phase, CancellationToken token)
    {
        await using var runtime = await LocalDemoRuntime.StartAsync(provider, token);
        await runtime.WaitForSamplesAsync(token);
        await using var browser = await LocalDemoBrowser.CreateAsync(engine);
        await browser.Page.GotoAsync(SampleBrowserJourney.Address);
        await browser.Page.GetByText(Waiting, new()
        {
            Exact = true
        }).WaitForAsync();
        phase(Setup);
        await runtime.InitializeAsync(token);
        await browser.EnrollAsync(runtime);
        await browser.SignInAsync(runtime);
        var registration = await SampleCatalogueJourney.CreateAsync(browser.Page);
        await runtime.ConfigureSampleAsync(registration.ClientId, registration.Secret, token);
        phase(Authentication);
        await SampleBrowserJourney.SignInAsync(browser.Page, runtime, browser.SampleFactorAsync, allow: false);
        await SampleBrowserJourney.SignInAsync(browser.Page, runtime, browser.SampleFactorAsync, allow: true);
        phase(Resource);
        await SampleBrowserJourney.ResourcesAsync(browser.Page, registration.Secrets);
        phase(Isolation);
        await using var independent = await browser.Context.Browser!.NewContextAsync();
        LocalDemoNavigation.Configure(independent);
        var second = await independent.NewPageAsync();
        await SampleBrowserJourney.SignInAsync(second, runtime, browser.SampleFactorAsync, allow: true);
        await SampleBrowserJourney.LogoutAsync(browser.Page);
        await SampleBrowserJourney.ResourcesAsync(second, registration.Secrets);
        await SampleCatalogueJourney.RemoveEmailAsync(browser.Page, registration.ClientId);
        await SampleBrowserJourney.ChangedPolicyAsync(second);
        return (await independent.CookiesAsync()).Select(cookie => new Cookie
        {
            Name = cookie.Name,
            Value = cookie.Value,
            Domain = cookie.Domain,
            Path = cookie.Path,
            HttpOnly = cookie.HttpOnly,
            Secure = cookie.Secure,
            SameSite = cookie.SameSite,
            Expires = cookie.Expires
        }).ToArray();
    }
}
