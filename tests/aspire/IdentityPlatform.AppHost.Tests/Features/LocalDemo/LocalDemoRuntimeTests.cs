using Microsoft.Playwright;

namespace IdentityPlatform.AppHost.Tests.Features.LocalDemo;

/// <summary>Requires the real container graph, stdin provisioning, trusted browser TLS and disposable restart on each provider.</summary>
public sealed class LocalDemoRuntimeTests
{
    private const string StartupPhase = "container startup";
    private const string InitializationPhase = "stdin initialization";
    private const string EnrollmentPhase = "password and TOTP enrollment";
    private const string LoginPhase = "fresh browser login";
    private const string LocalLogoutPhase = "local logout and replay denial";
    private const string SecondLoginPhase = "fresh second ceremony";
    private const string IssuerPositivePhase = "issuer cookie positive control";
    private const string IssuerLogoutPhase = "issuer logout confirmation and return";
    private const string IssuerDeniedPhase = "old issuer cookie denial";
    private const string DisposalPhase = "owned graph disposal";
    private const string RestartPhase = "new disposable run invalidation";
    private const string FailurePrefix = "Local demo verification failed during ";
    private const string FailureSuffix = ". No private browser or process output is retained.";
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(9);

    /// <summary>Normal runtime hosts perform genuine enrollment/fresh login/both logout paths and reject authentic cookies after a new run.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_DisposableContainerDemo_When_RealBrowserJourneysAndRestartRun_Then_AuthorityAndLifecycleRemainBounded(string provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(Deadline);
        var phase = StartupPhase;
        try
        {
            var cookies = await CompleteJourneysAsync(provider, value => phase = value, deadline.Token);
            phase = RestartPhase;
            await using var fresh = await LocalDemoRuntime.StartAsync(provider, deadline.Token);
            await using var replay = await LocalDemoBrowser.CreateAsync();
            await replay.RestoreAsync(cookies);
            await replay.RequireStatusAsync(false);
        }
        catch
        {
            // Native browser exceptions may contain private form values or protocol URLs.
            throw new InvalidOperationException(FailurePrefix + phase + FailureSuffix);
        }
    }
    /// <summary>Proves independent issuer revocation and retains a genuinely live final session for the new-run invalidation control.</summary>
    private static async Task<Cookie[]> CompleteJourneysAsync(string provider, Action<string> phase, CancellationToken token)
    {
        await using var runtime = await LocalDemoRuntime.StartAsync(provider, token);
        await using var browser = await LocalDemoBrowser.CreateAsync();
        phase(InitializationPhase);
        await runtime.InitializeAsync(token);
        phase(EnrollmentPhase);
        await browser.EnrollAsync(runtime);
        phase(LoginPhase);
        await browser.SignInAsync(runtime);
        var cookies = await browser.CookiesAsync();
        phase(LocalLogoutPhase);
        await browser.LogoutAsync(issuer: false);
        await browser.RequireClientReplayDeniedAsync(cookies);
        phase(SecondLoginPhase);
        await browser.SignInAsync(runtime);
        cookies = await browser.CookiesAsync();
        phase(IssuerPositivePhase);
        await browser.RequireIssuerReplayAsync(cookies, true);
        phase(IssuerLogoutPhase);
        await browser.LogoutAsync(issuer: true);
        phase(IssuerDeniedPhase);
        await browser.RequireIssuerReplayAsync(cookies, false);
        phase(LoginPhase);
        await browser.SignInAsync(runtime);
        await browser.RequireStatusAsync(true);
        var live = await browser.CookiesAsync();
        phase(DisposalPhase);
        return live;
    }

}
