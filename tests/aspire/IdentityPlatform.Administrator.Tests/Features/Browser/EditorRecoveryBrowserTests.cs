using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Adds visible recovery coverage without replacing retained editor, transport or native issuer identities.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
public sealed class EditorRecoveryBrowserTests(BootstrapFixture fixture)
{
    /// <summary>Real UI writes precede finite response seams, genuine lost output and required keyboard/theme/reflow/axe checks.</summary>
    [Theory]
    [Trait("BrowserStory", "ApplicationEditor")]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_RealManagedEditor_When_RecoveryAndWorkingStatesArePresented_Then_ExplicitActionsPreserveVersionAndSecretBoundaries(string provider) =>
        ApplicationBrowserDiagnostics.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true, managed: true);
            await using var browser = await BrowserSession.CreateAsync();
            var page = await browser.Context.NewPageAsync();
            await ApplicationAccessFixture.SignInAsync(environment, page);
            await page.Clock.SetFixedTimeAsync(environment.Issuer.Account.Journey.Clock.Now.UtcDateTime);
            await using var observation = await ApplicationLifecycleObservation.CreateAsync(page);
            var secrets = await EditorRecoveryJourney.CreateAndChangeAsync(environment, page);
            var id = await EditorRecoveryStore.IdAsync(environment);
            await EditorRecoveryFailures.FiniteAsync(environment, page, id);
            await EditorRecoveryWorking.RequireAsync(environment, page, id);
            var lost = await EditorRecoveryFailures.LostOutputAsync(environment, page, id, secrets);
            await observation.RequireAsync([.. secrets, lost]);
            return [.. secrets, lost];
        });

    /// <summary>Persisted permission removal and the real original access deadline produce the shipped denial/re-authentication states.</summary>
    [Theory]
    [Trait("BrowserStory", "ApplicationAuthority")]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_AuthenticatedEditor_When_CurrentPermissionsOrOriginalAuthorityExpire_Then_NoMutationCommitsAndRecoveryIsAccessible(string provider) =>
        ApplicationBrowserDiagnostics.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true, applications: true, managed: true);
            await using var browser = await BrowserSession.CreateAsync();
            var page = await browser.Context.NewPageAsync();
            var access = await ApplicationAccessFixture.SignInAsync(environment, page);
            await page.Clock.SetFixedTimeAsync(environment.Issuer.Account.Journey.Clock.Now.UtcDateTime);
            await using var observation = await ApplicationLifecycleObservation.CreateAsync(page);
            var secrets = await EditorRecoveryJourney.RegisterAsync(environment, page);
            var id = await EditorRecoveryStore.IdAsync(environment);
            await EditorRecoveryFailures.PermissionsAsync(environment, page, id);
            await EditorRecoveryFailures.ExpiryAsync(environment, page, id, access.ExpiresAt);
            await BrowserSecretOracle.StorageAsync(page, secrets);
            await observation.RequireAsync(secrets);
            return secrets;
        });
}
