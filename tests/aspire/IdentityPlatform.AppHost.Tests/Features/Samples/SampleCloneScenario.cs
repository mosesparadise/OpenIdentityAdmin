using IdentityPlatform.Administrator.Tests.Features.Browser;

namespace IdentityPlatform.AppHost.Tests.Features.Samples;

/// <summary>Requires a UI-cloned disabled application to be explicitly enabled before fresh password/MFA/consent and real sample API use.</summary>
internal static class SampleCloneScenario
{
    private const int DeadlineMinutes = 9;
    private const string Startup = "clone sample readiness";
    private const string Setup = "clone source catalogue and administrator setup";
    private const string Clone = "reviewed clone form";
    private const string Disabled = "disabled clone native admission";
    private const string Enable = "explicit clone enablement";
    private const string SignIn = "clone native password MFA and consent";
    private const string Resources = "clone UserInfo and independently validating API";
    private const string Failure = "Clone sample verification failed during ";
    private const string Safe = ". Private browser, credential and protocol output was not retained.";

    /// <summary>Owns ordinary Aspire startup and the complete genuine client/resource flow for each provider/browser matrix cell.</summary>
    internal static async Task RunAsync(string provider, string engine)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(DeadlineMinutes));
        var phase = Startup;
        try
        {
            await using var runtime = await LocalDemoRuntime.StartAsync(provider, deadline.Token);
            await runtime.WaitForSamplesAsync(deadline.Token);
            await using var browser = await LocalDemoBrowser.CreateAsync(engine);
            phase = Setup;
            await runtime.InitializeAsync(deadline.Token);
            await browser.EnrollAsync(runtime);
            await browser.SignInAsync(runtime);
            var source = await SampleCatalogueJourney.CreateAsync(browser.Page);
            phase = Clone;
            var clone = await SampleCloneJourney.RunAsync(browser.Page, source);
            phase = Disabled;
            await ApplicationStateJourney.RequireDeniedAsync(browser.Page, SampleCloneJourney.Issuer, clone);
            phase = Enable;
            await ApplicationStateJourney.ChangeAsync(browser.Page, SampleCloneJourney.Administrator, clone, enabled: true, previousRevision: 1);
            await ApplicationCredentialEvidence.RequireAsync(SampleCloneJourney.Issuer, clone.ClientId, clone.Secret, clone.Callback, rejectedSecret: source.Secret);
            await runtime.ConfigureSampleAsync(clone.ClientId, clone.Secret, deadline.Token);
            phase = SignIn;
            await SampleBrowserJourney.SignInAsync(browser.Page, runtime, browser.SampleFactorAsync, allow: true);
            phase = Resources;
            await SampleBrowserJourney.ResourcesAsync(browser.Page, [.. source.Secrets, clone.Secret]);
            await ApplicationCredentialEvidence.RequireAsync(SampleCloneJourney.Issuer, source.ClientId, source.Secret, source.Callback);
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(Failure + phase + Safe + LocalDemoFailureEvidence.Describe(error));
        }
    }
}
