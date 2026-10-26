using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Uses actual cross-site forms and fetch plus independent same-origin controls to prove browser request boundaries.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class SessionOriginBrowserTests(BootstrapFixture fixture)
{
    private const string Form = "form";
    private const string Submit = "button[type=submit]";
    private const string CopyForm = "(form, destination) => { const copy=form.cloneNode(true); copy.action=destination; return copy.outerHTML; }";
    private const string StatusPath = "/bff/session";
    private const string Fetch = "async url => { try { await fetch(url, {credentials:'include'}); return false; } catch { return true; } }";
    private const string RemoveProof = "form => form.querySelector('input[name=__RequestVerificationToken]').remove()";
    private const string LoginPost = "/bff/login";

    /// <summary>Actual foreign-origin login/replacement POSTs cannot mutate state even with a real form token; cross-site fetch reaches the server without authority but cannot read its result.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_DistinctTrustedSites_When_LoginFormsAndStatusAreRequestedCrossSite_Then_StateAndCookiesStayProtected(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, distinctSites: true);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            var attacker = await session.Context.NewPageAsync();
            await page.GotoAsync(new Uri(environment.Profile.ClientUri, BrowserHttpsJourney.Login).AbsoluteUri);
            await AttackAsync(environment, page, attacker);
            await BrowserHttpsJourney.SignInAsync(environment, page);
            await page.GotoAsync(new Uri(environment.Profile.ClientUri, BrowserHttpsJourney.Login).AbsoluteUri);
            await AttackAsync(environment, page, attacker);
            var before = await BrowserHttpsState.SnapshotAsync(environment);
            await attacker.GotoAsync(environment.StageAttacker(environment.Profile.Issuer, string.Empty, fetch: true).AbsoluteUri);
            Assert.True(await attacker.EvaluateAsync<bool>(Fetch, new Uri(environment.Profile.ClientUri, StatusPath).AbsoluteUri));
            Assert.Equal(1, environment.Status.CrossOrigin);
            Assert.Equal(1, environment.Status.Completed);
            Assert.Equal(0, environment.Status.WrongPeer);
            Assert.Equal(0, environment.Status.InvalidResponse);
            Assert.Equal(0, environment.Status.Bearer);
            Assert.Equal(0, environment.Status.AuthenticatedCookies);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            await page.Locator(Form).EvaluateAsync(RemoveProof);
            var denied = await page.RunAndWaitForResponseAsync(() => page.Locator(Submit).ClickAsync(), response => response.Request.Method == HttpMethods.Post);
            Assert.Equal(StatusCodes.Status400BadRequest, denied.Status);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, true);
            await BrowserHttpsJourney.SignInAsync(environment, page);
            Assert.Equal(TwoLogins, environment.TokenRequests);
            environment.RequirePolicies(ClientCounts, IssuerCounts);
        });

    /// <summary>Copies the actual original protected form into a distinct-site document; native POST origin and cookie policy remain browser-owned.</summary>
    private static async Task AttackAsync(BrowserHttpsFixture fixture, IPage original, IPage attacker)
    {
        var before = await BrowserHttpsState.SnapshotAsync(fixture);
        var form = await original.Locator(Form).EvaluateAsync<string>(CopyForm, new Uri(fixture.Profile.ClientUri, LoginPost).AbsoluteUri);
        await attacker.GotoAsync(fixture.StageAttacker(fixture.Profile.Issuer, form).AbsoluteUri);
        var denied = await attacker.RunAndWaitForResponseAsync(() => attacker.Locator(Submit).ClickAsync(), response => response.Request.Method == HttpMethods.Post);
        Assert.Equal(StatusCodes.Status400BadRequest, denied.Status);
        Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(fixture));
    }
    private const int TwoLogins = 2;
    private static readonly BrowserHttpsCounts ClientCounts = new(5, 4, 6, 6, 2);
    private static readonly BrowserHttpsCounts IssuerCounts = new(4, 2, 4, 6);
}
