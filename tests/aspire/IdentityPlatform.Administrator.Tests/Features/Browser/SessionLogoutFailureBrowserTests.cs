using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Issuer.Tests.Features.Browser;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Distinguishes rollback from real committed-but-unacknowledged logout through native browser forms and real provider transactions.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserSessions")]
public sealed class SessionLogoutFailureBrowserTests(BootstrapFixture fixture)
{
    private const int ExplicitLogoutAttempts = 2;
    private const string Failure = "Synthetic browser logout after-save failure.";

    /// <summary>A fault after the real save rolls back ticket and audit writes; a later explicit unfaulted browser action succeeds.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_AuthenticatedBrowser_When_LocalSaveRollsBack_Then_UnconfirmedResponsePreservesOriginalSession(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            var fault = new BrowserRemoteLogoutFault(BrowserMutation.Logout) { AfterSave = () => throw new InvalidOperationException(Failure) };
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, fault.Configure);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await BrowserHttpsJourney.SignInAsync(environment, page);
            var source = await BrowserHttpsEffects.SourceAsync(environment);
            var before = await BrowserHttpsState.SnapshotAsync(environment);
            await BrowserLogoutJourney.UnconfirmedAsync(environment, page);
            Assert.Equal(1, fault.Saves);
            Assert.Equal(0, fault.Commits);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            await BrowserHttpsJourney.CookiesAsync(environment, page);
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, true);
            await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: false);
            fault.AfterSave = null;
            await BrowserHttpsJourney.LogoutAsync(environment, page, issuer: false);
            Assert.Equal(2, fault.Saves);
            Assert.Equal(1, fault.Commits);
            await BrowserHttpsEffects.LocalAsync(environment, source, null);
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, false);
            environment.RequirePolicies(logins: 1, logouts: ExplicitLogoutAttempts, confirmations: 0);
        });

    /// <summary>Lost commit acknowledgement reports uncertainty without cookie deletion; explicit reconciliation observes the existing commit without another mutation.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_RealLocalCommit_When_AcknowledgementIsLost_Then_UnconfirmedBrowserCanExplicitlyReconcileWithoutReissuingAuthority(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            var fault = new BrowserRemoteLogoutFault(BrowserMutation.Logout) { LoseAcknowledgement = true };
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, fault.Configure);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await BrowserHttpsJourney.SignInAsync(environment, page);
            var source = await BrowserHttpsEffects.SourceAsync(environment);
            await BrowserLogoutJourney.UnconfirmedAsync(environment, page);
            Assert.Equal(1, fault.Saves);
            Assert.Equal(1, fault.Commits);
            Assert.Equal(1, fault.Cleanups);
            await BrowserHttpsEffects.LocalAsync(environment, source, null);
            var committed = await BrowserHttpsState.SnapshotAsync(environment);
            await BrowserHttpsJourney.LogoutAsync(environment, page, issuer: false);
            Assert.Equal(committed, await BrowserHttpsState.SnapshotAsync(environment));
            Assert.Equal(1, fault.Commits);
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, false);
            await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: false);
            environment.RequirePolicies(logins: 1, logouts: ExplicitLogoutAttempts, confirmations: 0);
        });
}
