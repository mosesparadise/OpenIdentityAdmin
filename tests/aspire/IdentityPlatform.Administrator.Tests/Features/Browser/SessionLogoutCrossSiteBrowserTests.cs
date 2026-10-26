using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Uses trusted localhost and loopback-IP sites to prove actual host-only cookies, inert GET and cross-site native POST denial.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserSessions")]
public sealed class SessionLogoutCrossSiteBrowserTests(BootstrapFixture fixture)
{
    private static readonly BrowserHttpsCounts ClientCounts = new(3, 3, 4, 3, 1);
    private static readonly BrowserHttpsCounts IssuerCounts = new(4, 1, 3, 4, 1);
    private const string Form = "form";
    private const string CopyForm = "(form, destination) => { const copy = form.cloneNode(true); copy.action = destination; return copy.outerHTML; }";
    private const string LocalChoice = "button[name=Mode][value=local]";
    private const string Submit = "button[type=submit]";

    /// <summary>Attacker forms carry the real token but neither host's Lax authentication cookies; original same-origin forms and top-level returns still work.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_DistinctTrustedSites_When_RealFormsArePostedCrossSite_Then_NeitherLogoutMutatesAndOriginalFormsStillSucceed(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, distinctSites: true);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            Assert.NotEqual(environment.Profile.Issuer.Host, environment.Profile.ClientUri.Host);
            await BrowserHttpsJourney.SignInAsync(environment, page);
            await BrowserHttpsJourney.CookiesAsync(environment, page);
            var source = await BrowserHttpsEffects.SourceAsync(environment);
            var before = await BrowserHttpsState.SnapshotAsync(environment);
            var local = new Uri(environment.Profile.ClientUri, BrowserHttpsJourney.Logout);
            await page.GotoAsync(local.AbsoluteUri);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            var attacker = await session.Context.NewPageAsync();
            await AttackAsync(environment, attacker, page, environment.Profile.Issuer, local, LocalChoice);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, true);
            await BrowserHttpsJourney.LogoutAsync(environment, page, issuer: true);
            var link = await BrowserLogoutJourney.LinkAsync(environment, page);
            await BrowserHttpsEffects.LocalAsync(environment, source, BrowserLogoutState.RemotePending);
            await page.GotoAsync(link.AbsoluteUri);
            await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: false);
            await AttackAsync(environment, attacker, page, environment.Profile.ClientUri, new Uri(environment.Profile.Issuer, BrowserLogoutJourney.IssuerPath), Submit);
            await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: false);
            await BrowserLogoutJourney.ConfirmAsync(environment, page);
            await BrowserHttpsEffects.LocalAsync(environment, source, BrowserLogoutState.ReturnedUnverified);
            await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: true);
            await BrowserHttpsJourney.RequireCookiesAsync(environment, page, issuer: false, remote: false);
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, false);
            Assert.Equal(1, environment.EndSessionGets);
            Assert.Equal(2, environment.EndSessionPosts);
            environment.RequirePolicies(ClientCounts, IssuerCounts);
        });

    /// <summary>Copies the legitimate form into a real other-site document and submits natively without overriding headers, cookies or TLS.</summary>
    private static async Task AttackAsync(BrowserHttpsFixture fixture, IPage attacker, IPage original, Uri foreignOrigin, Uri destination, string button)
    {
        var form = await original.Locator(Form).EvaluateAsync<string>(CopyForm, destination.AbsoluteUri);
        await attacker.GotoAsync(fixture.StageAttacker(foreignOrigin, form).AbsoluteUri);
        var response = await attacker.RunAndWaitForResponseAsync(() => attacker.Locator(button).ClickAsync(),
            response => response.Request.Method == HttpMethods.Post);
        if (destination.Host == fixture.Profile.Issuer.Host)
        {
            await BrowserLogoutJourney.RequireIssuerDeniedAsync(response);
        }
        else
        {
            Assert.Equal(StatusCodes.Status400BadRequest, response.Status);
        }
        Assert.True(Uri.TryCreate(attacker.Url, UriKind.Absolute, out var uri) && uri.GetLeftPart(UriPartial.Path) == destination.AbsoluteUri);
    }
}
