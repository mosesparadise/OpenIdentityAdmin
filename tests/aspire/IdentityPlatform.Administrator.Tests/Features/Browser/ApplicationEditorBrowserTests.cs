using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Retains actual editor journeys on both providers for each configured retained engine; no mocked mutation endpoint satisfies these tests.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class ApplicationEditorBrowserTests(BootstrapFixture fixture)
{
    /// <summary>Genuine native administrator login precedes real create/edit/conflict/replacement, keyboard, secrecy and presentation checks.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_AuthenticatedAdministrator_When_EditorCreationConflictAndReplacementRun_Then_RealMutationsAndBoundedAccessibleOutputAreObserved(string provider) =>
        ApplicationBrowserDiagnostics.RunAsync(() => ApplicationManagedBrowserEvidence.RunAsync(fixture, provider, approve: true));
}
