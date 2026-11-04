using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Proves explicit managed consent cancellation for the exact registration created and edited by the real administrator UI.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "ManagedConsent")]
public sealed class ApplicationConsentBrowserTests(BootstrapFixture fixture)
{
    /// <summary>Fresh browser password/MFA followed by cancellation creates no code, token exchange or new administrator authority.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_UiCreatedAndEditedRegistration_When_BrowserConsentIsCancelled_Then_NoCredentialsOrNewAuthorityAreIssued(string provider) =>
        ApplicationBrowserDiagnostics.RunAsync(() => ApplicationManagedBrowserEvidence.RunAsync(fixture, provider, approve: false));
}
