using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Separates actual issuer failure and lost browser navigation from the independently committed local logout.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserSessions")]
public sealed class SessionLogoutRemoteBrowserTests(BootstrapFixture fixture)
{
    private static readonly BrowserHttpsCounts RecoveredClientCounts = new(2, 3, 3, 3);
    private static readonly BrowserHttpsCounts RecoveredIssuerCounts = new(4, 1, 4, 4);
    private static readonly BrowserHttpsCounts LostClientCounts = new(2, 2, 3, 3);
    private static readonly BrowserHttpsCounts LostIssuerCounts = new(3, 1, 3, 4);
    private const string Failure = "Synthetic issuer browser after-save failure.";

    /// <summary>A real issuer save fault rolls back issuer revocation while preserving local logout; explicit subsequent confirmation remains usable.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_KnownLocalLogout_When_IssuerSaveFails_Then_LocalRemainsRevokedAndRestoredExplicitConfirmationCommits(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            var fault = new IssuerLogoutFault { AfterSave = () => throw new InvalidOperationException(Failure) };
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, configureIssuer: fault.Configure);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            var source = await BrowserLogoutJourney.BeginAsync(environment, page);
            var link = await BrowserLogoutJourney.LinkAsync(environment, page);
            var local = await BrowserHttpsState.SnapshotAsync(environment);
            await page.GotoAsync(link.AbsoluteUri);
            var response = await page.RunAndWaitForResponseAsync(() => page.GetByRole(AriaRole.Button,
                new()
                {
                    Name = BrowserLogoutJourney.Confirm,
                    Exact = true
                }).ClickAsync(),
                response => response.Request.Method == HttpMethods.Post);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.Status);
            Assert.Equal(1, fault.Saves);
            Assert.Equal(0, fault.Commits);
            await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: false);
            Assert.Equal(local, await BrowserHttpsState.SnapshotAsync(environment));
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, false);
            fault.AfterSave = null;
            await page.GotoAsync(link.AbsoluteUri);
            await BrowserLogoutJourney.ConfirmAsync(environment, page);
            Assert.Equal(2, fault.Saves);
            Assert.Equal(1, fault.Commits);
            await BrowserHttpsEffects.LocalAsync(environment, source, BrowserLogoutState.ReturnedUnverified);
            await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: true);
            Assert.Equal(2, environment.EndSessionGets);
            Assert.Equal(2, environment.EndSessionPosts);
            environment.RequirePolicies(RecoveredClientCounts, RecoveredIssuerCounts);
        });

    /// <summary>Actual issuer revocation survives a dropped ensuing return request; no BFF transition occurs until a later explicit original return.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_ConfirmedIssuerLogout_When_ReturnNavigationIsLost_Then_BothRevocationsPersistWithoutInventedBffAcknowledgement(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            var source = await BrowserLogoutJourney.BeginAsync(environment, page);
            var link = await BrowserLogoutJourney.LinkAsync(environment, page);
            await page.GotoAsync(link.AbsoluteUri);
            var loss = new BrowserLostReturn(environment);
            await loss.InstallAsync(page);
            var before = await BrowserHttpsState.SnapshotAsync(environment);
            await loss.ConfirmAsync(page);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            await BrowserHttpsEffects.LocalAsync(environment, source, BrowserLogoutState.RemotePending);
            await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: true);
            await BrowserHttpsJourney.RequireCookiesAsync(environment, page, issuer: false, remote: true);
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, false);
            environment.RequirePolicies(LostClientCounts with
            {
                Returns = LostClientCounts.Returns + loss.AbortedRequests
            }, LostIssuerCounts);
            await page.GotoAsync(loss.Destination!.AbsoluteUri);
            await BrowserLogoutJourney.RequireReturnedAsync(environment, page);
            await BrowserHttpsEffects.LocalAsync(environment, source, BrowserLogoutState.ReturnedUnverified);
            await BrowserHttpsJourney.RequireCookiesAsync(environment, page, issuer: false, remote: false);
            await BrowserLogoutJourney.DeniedAsync(page, loss.Destination);
            Assert.Equal(1, environment.EndSessionGets);
            Assert.Equal(1, environment.EndSessionPosts);
            environment.RequirePolicies(logins: 1, logouts: 1, confirmations: 1, issuerVisits: 1, returnReloads: 1 + loss.AbortedRequests);
        });
}
