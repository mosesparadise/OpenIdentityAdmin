using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Requires real retirement recovery in each retained provider/browser cell through shipped UI and native grant state.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class ApplicationRetirementBrowserTests(BootstrapFixture fixture)
{
    /// <summary>Lost committed acceptance is recovered after full reload and actual logout/password/MFA with no browser key or automatic mutation replay.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_LostRetirementResponse_When_UserReloadsAndSignsInAgain_Then_RetiredNavigationRecoversOnlyOriginalJob(string provider) =>
        ApplicationBrowserDiagnostics.RunAsync(() => ApplicationManagedBrowserEvidence.RunAsync(fixture, provider, approve: true, revoke: true,
            recovery: RetirementBrowserJourney.LostAsync));

    /// <summary>Real stopped progress remains reachable after relogin; stale continuation fails and fresh confirmation completes without re-enabling.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_StoppedRetirement_When_UserRelogsAndConfirmsCurrentVersions_Then_OriginalJobCompletesAndHistoryRemains(string provider) =>
        ApplicationBrowserDiagnostics.RunAsync(() => ApplicationManagedBrowserEvidence.RunAsync(fixture, provider, approve: true, revoke: true,
            recovery: RetirementBrowserJourney.StoppedAsync));
}
