using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Requires fresh real password/MFA for live-session replacement and rejects stale browser operation material.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserLogin")]
public sealed class SessionLoginBrowserTests(BootstrapFixture fixture)
{
    private const string Start = "Start sign-in";
    private const int TwoLogins = 2;
    private static readonly BrowserHttpsCounts ClientCounts = new(3, 4, 5, 6);
    private static readonly BrowserHttpsCounts IssuerCounts = new(4, 2, 4, 6);

    /// <summary>Valid BFF and issuer cookies cannot skip either fresh proof; replacement revokes the prior ticket and an actual old tab cannot restart it.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_LiveBffAndIssuerSessions_When_ExplicitSignInReplacesThem_Then_FreshCeremonyCommitsAndOldCookieAndTabDeny(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync();
            var stale = await session.Context.NewPageAsync();
            await BrowserHttpsJourney.SignInAsync(environment, stale);
            var cookies = await BrowserHttpsJourney.CookiesAsync(environment, stale);
            var source = await BrowserHttpsEffects.SourceAsync(environment);
            var proofs = await BrowserLoginEffects.CeremoniesAsync(environment);
            await stale.GotoAsync(new Uri(environment.Profile.ClientUri, BrowserHttpsJourney.Login).AbsoluteUri);
            var current = await session.Context.NewPageAsync();
            await FreshReplacementAsync(environment, current, proofs);
            await BrowserLoginEffects.ReplacementAsync(environment, source);
            await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: false);
            await using var replay = await BrowserSession.CreateAsync();
            await replay.Context.AddCookiesAsync(cookies);
            await BrowserHttpsJourney.RequireStatusAsync(environment, await replay.Context.NewPageAsync(), false);
            var before = await BrowserHttpsState.SnapshotAsync(environment);
            var denied = await stale.RunAndWaitForResponseAsync(() => stale.GetByRole(AriaRole.Button,
                new()
                {
                    Name = Start,
                    Exact = true
                }).ClickAsync(), response => response.Request.Method == HttpMethods.Post);
            Assert.Equal(StatusCodes.Status400BadRequest, denied.Status);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            await BrowserHttpsJourney.RequireStatusAsync(environment, current, true);
            Assert.Equal(TwoLogins, environment.ParRequests);
            Assert.Equal(TwoLogins, environment.TokenRequests);
            environment.RequirePolicies(ClientCounts, IssuerCounts);
        });

    /// <summary>Checks progress before and after each actual password/factor POST; no new native exchange exists before both proofs.</summary>
    private static async Task FreshReplacementAsync(BrowserHttpsFixture fixture, IPage page, (int Passwords, int Factors) before)
    {
        await BrowserHttpsJourney.BeginAsync(fixture, page);
        Assert.Equal(before, await BrowserLoginEffects.CeremoniesAsync(fixture));
        Assert.Equal(1, fixture.ParRequests);
        Assert.Equal(1, fixture.TokenRequests);
        await BrowserHttpsJourney.PasswordAsync(fixture, page);
        Assert.Equal((before.Passwords + 1, before.Factors), await BrowserLoginEffects.CeremoniesAsync(fixture));
        Assert.Equal(1, fixture.ParRequests);
        Assert.Equal(1, fixture.TokenRequests);
        await BrowserHttpsJourney.FactorAsync(fixture, page);
        await BrowserHttpsJourney.FinishAsync(fixture, page);
        Assert.Equal((before.Passwords + 1, before.Factors + 1), await BrowserLoginEffects.CeremoniesAsync(fixture));
    }
}
