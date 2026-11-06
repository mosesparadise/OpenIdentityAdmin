using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Requires retained browser engines to recover real accepted and stopped jobs through shipped UI and native provider state.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class ApplicationGrantRecoveryBrowserTests(BootstrapFixture fixture)
{
    /// <summary>Lost committed output and current permission denial never invent a second intent or automatic mutation retry.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_LostCommittedAcceptance_When_UserExplicitlyRecoversAfterPermissionRestoration_Then_SameIntentAndOneApprovalComplete(string provider) =>
        ApplicationBrowserDiagnostics.RunAsync(() => ApplicationManagedBrowserEvidence.RunAsync(fixture, provider, approve: true, revoke: true,
            recovery: GrantRecoveryBrowserJourney.LostAsync));

    /// <summary>Partial progress, real authority stops and a stale competing confirmation require freshly read versions and a new explicit approval.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_StoppedJobAndCompetingContinuation_When_UserReloadsAndConfirmsFreshVersions_Then_SameJobCompletesWithImmutableHistory(string provider) =>
        ApplicationBrowserDiagnostics.RunAsync(() => ApplicationManagedBrowserEvidence.RunAsync(fixture, provider, approve: true, revoke: true,
            recovery: GrantRecoveryBrowserJourney.StoppedAsync));
}
