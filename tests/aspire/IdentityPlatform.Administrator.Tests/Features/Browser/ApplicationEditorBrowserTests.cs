using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;

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
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true, managed: true);
            await using var browser = await BrowserSession.CreateAsync();
            var page = await browser.Context.NewPageAsync();
            await ApplicationAccessFixture.SignInAsync(environment, page);
            await ApplicationEditorJourney.RunAsync(page, environment.Profile.ClientUri, environment.Issuer.Account.Journey.Clock.Now, WorkspacePresentation.RequireStateAsync);
        });
}
