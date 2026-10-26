using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Proves genuine B3 login and independent logout effects across real HTTPS hosts and the selected browser engine.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserSessions")]
public sealed class SessionLogoutBrowserTests(BootstrapFixture fixture)
{
    private static readonly BrowserHttpsCounts AbandonedClientCounts = new(2, 2, 4, 3);
    private static readonly BrowserHttpsCounts AbandonedIssuerCounts = new(2, 1, 3, 3);
    private const string Main = "main";
    private const string LinkAttribute = "href";
    private const string IssuerLink = "#issuer-sign-out";
    private const string Confirm = "Sign out this issuer session";
    private const string Unverified = "Issuer sign-out result is unverified.";
    private const string IssuerPath = "/connect/end-session";
    private const string Cancel = "Return without issuer sign-out";
    private const int SecondExchange = 2;

    /// <summary>Actual local revocation denies retained authentic cookies while the issuer stays live and the next sign-in still requires fresh password/MFA.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_RealHttpsSession_When_LocalLogoutAndFreshSignIn_Then_OldCookiesDenyAndIssuerAuthorityIsSeparate(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await BrowserHttpsJourney.SignInAsync(environment, page);
            var cookies = await BrowserHttpsJourney.CookiesAsync(environment, page);
            var source = await BrowserHttpsEffects.SourceAsync(environment);
            await BrowserHttpsJourney.LogoutAsync(environment, page, issuer: false);
            await BrowserHttpsJourney.RequireCookiesAsync(environment, page, issuer: true, remote: false);
            await BrowserHttpsEffects.LocalAsync(environment, source, null);
            await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: false);
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, false);
            await using (var replay = await BrowserSession.CreateAsync())
            {
                await replay.Context.AddCookiesAsync(cookies);
                await BrowserHttpsJourney.RequireStatusAsync(environment, await replay.Context.NewPageAsync(), false);
            }
            Assert.Equal(1, environment.ParRequests);
            Assert.Equal(1, environment.TokenRequests);
            await BrowserHttpsJourney.SignInAsync(environment, page);
            Assert.True(await BrowserHttpsEffects.SourceAsync(environment) != source);
            Assert.Equal(SecondExchange, environment.ParRequests);
            Assert.Equal(SecondExchange, environment.TokenRequests);
            Assert.Equal(0, environment.EndSessionGets);
            Assert.Equal(0, environment.EndSessionPosts);
            environment.RequirePolicies(logins: SecondExchange, logouts: 1, confirmations: 0);
        });

    /// <summary>Native issuer GET is inert; protected confirmation revokes the exact source, while the one-use BFF return remains unverified.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_RealHttpsSession_When_IssuerConfirmationCompletes_Then_BothCommitsAreObservedAndReturnRemainsUnverified(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            var source = await ConfirmPageAsync(environment, page);
            await page.GetByRole(AriaRole.Button, new()
            {
                Name = Confirm,
                Exact = true
            }).ClickAsync();
            await page.Locator(Main).Filter(new()
            {
                HasText = Unverified
            }).WaitForAsync();
            Assert.True(Uri.TryCreate(page.Url, UriKind.Absolute, out var uri) &&
                uri.GetLeftPart(UriPartial.Path) == new Uri(environment.Profile.ClientUri, BrowserHttpsJourney.Return).AbsoluteUri);
            Assert.True((await page.TextContentAsync(Main))!.Contains(Unverified, StringComparison.Ordinal));
            await BrowserHttpsEffects.LocalAsync(environment, source, BrowserLogoutState.ReturnedUnverified);
            await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: true);
            await BrowserHttpsJourney.RequireCookiesAsync(environment, page, issuer: false, remote: false);
            Assert.Equal(1, environment.EndSessionGets);
            Assert.Equal(1, environment.EndSessionPosts);
            var replay = await page.ReloadAsync();
            Assert.Equal(StatusCodes.Status400BadRequest, replay!.Status);
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, false);
            Assert.Equal(1, environment.ParRequests);
            Assert.Equal(1, environment.TokenRequests);
            environment.RequirePolicies(logins: 1, logouts: 1, confirmations: 1, issuerVisits: 1, returnReloads: 1);
        });

    /// <summary>Abandoning the real native confirmation cannot restore local authority or claim issuer revocation.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_RealHttpsSession_When_IssuerConfirmationIsAbandoned_Then_LocalRevocationPersistsAndIssuerRemainsLive(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            var source = await ConfirmPageAsync(environment, page);
            await page.GetByRole(AriaRole.Link, new()
            {
                Name = Cancel,
                Exact = true
            }).ClickAsync();
            Assert.True(page.Url == new Uri(environment.Profile.ClientUri, BrowserHttpsJourney.Login).AbsoluteUri);
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, false);
            await BrowserHttpsEffects.LocalAsync(environment, source, BrowserLogoutState.RemotePending);
            Assert.Equal(1, environment.EndSessionGets);
            Assert.Equal(0, environment.EndSessionPosts);
            await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: false);
            Assert.Equal(1, environment.ParRequests);
            Assert.Equal(1, environment.TokenRequests);
            environment.RequirePolicies(AbandonedClientCounts, AbandonedIssuerCounts);
        });

    /// <summary>Follows only the actual BFF-produced issuer link after proving local commit and verifies the loaded native GET did not revoke.</summary>
    private static async Task<string> ConfirmPageAsync(BrowserHttpsFixture environment, IPage page)
    {
        await BrowserHttpsJourney.SignInAsync(environment, page);
        await BrowserHttpsJourney.CookiesAsync(environment, page);
        var source = await BrowserHttpsEffects.SourceAsync(environment);
        await BrowserHttpsJourney.LogoutAsync(environment, page, issuer: true);
        await BrowserHttpsJourney.RequireCookiesAsync(environment, page, issuer: true, remote: true);
        await BrowserHttpsEffects.LocalAsync(environment, source, BrowserLogoutState.RemotePending);
        await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: false);
        var link = await page.Locator(IssuerLink).GetAttributeAsync(LinkAttribute);
        Assert.True(Uri.TryCreate(link, UriKind.Absolute, out var uri) &&
            uri.GetLeftPart(UriPartial.Path) == new Uri(environment.Profile.Issuer, IssuerPath).AbsoluteUri);
        await page.Locator(IssuerLink).ClickAsync();
        await page.GetByRole(AriaRole.Button, new()
        {
            Name = Confirm,
            Exact = true
        }).WaitForAsync();
        await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: false);
        return source;
    }
}
