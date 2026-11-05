using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Requires genuine issuer-issued grants, shipped React confirmation, real background Worker processing and independent provider observations.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class ApplicationGrantBrowserTests(BootstrapFixture fixture)
{
    /// <summary>Retained engines execute keyboard/focus and both-theme checks while the actual Worker revokes precisely the selected client's issued native entries.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_GenuineManagedGrants_When_ConfirmedInBrowser_Then_WorkerCompletesWithExactVisibleAndPersistedProgress(string provider) =>
        ApplicationBrowserDiagnostics.RunAsync(() => ApplicationManagedBrowserEvidence.RunAsync(fixture, provider, approve: true, revoke: true));
}
