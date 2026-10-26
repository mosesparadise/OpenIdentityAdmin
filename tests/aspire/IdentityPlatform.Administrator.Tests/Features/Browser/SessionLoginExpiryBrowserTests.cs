using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Separates expired intent, native PAR and protected client-state journeys at their actual browser request stages.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserLogin")]
public sealed class SessionLoginExpiryBrowserTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private const string ResumePath = "/bff/login/resume";
    private const string AuthorizePath = "/connect/authorize";
    private const string CallbackPath = "/callback";
    private static readonly BrowserNavigationStage[] Stages = [BrowserNavigationStage.ResumeRequest,
        BrowserNavigationStage.Authorization, BrowserNavigationStage.Callback];

    /// <summary>Discovers three independent original-boundary cases on each actual provider.</summary>
    public static IEnumerable<TheoryDataRow<string, BrowserNavigationStage>> Cases =>
        from provider in IssuerContinuationProviderFixture.Providers
        from stage in Stages
        select new TheoryDataRow<string, BrowserNavigationStage>(provider, stage).WithTrait(ProviderTrait, provider);

    /// <summary>The real request reaches its selected stage, then equality at its stored deadline prevents a ticket and any token exchange.</summary>
    [Theory, MemberData(nameof(Cases))]
    public Task Given_OriginalStoredDeadline_When_RealBrowserArrivesAtExpiry_Then_NoExchangeOrTicketIsPublished(string provider, BrowserNavigationStage stage) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await BrowserHttpsJourney.BeginAsync(environment, page);
            await BrowserHttpsJourney.PasswordAsync(environment, page);
            var issuer = stage == BrowserNavigationStage.Authorization;
            var path = stage == BrowserNavigationStage.ResumeRequest ? ResumePath : issuer ? AuthorizePath : CallbackPath;
            var target = new Uri(issuer ? environment.Profile.Issuer : environment.Profile.ClientUri, path);
            var pause = issuer ? environment.IssuerNavigation : environment.ClientNavigation;
            IResponse? response = null;
            await pause.RunAsync(stage, async () => response = await page.RunAndWaitForResponseAsync(
                () => BrowserHttpsJourney.FactorAsync(environment, page), item => item.Request.Method == HttpMethods.Get &&
                    Uri.TryCreate(item.Url, UriKind.Absolute, out var uri) && uri.GetLeftPart(UriPartial.Path) == target.AbsoluteUri),
                uri => ExpireAsync(environment, stage, uri));
            Assert.Equal(stage == BrowserNavigationStage.Callback ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status400BadRequest, response!.Status);
            Assert.False(response.Headers.ContainsKey(Location));
            Assert.Equal(0, environment.TokenRequests);
            Assert.Equal(stage == BrowserNavigationStage.ResumeRequest ? 0 : 1, environment.ParRequests);
            await BrowserLoginEffects.NoTicketAsync(environment);
            RequirePolicies(environment, stage);
        });

    /// <summary>Applies only a test-clock change using the independently read deadline for this actual held request.</summary>
    private static Task ExpireAsync(BrowserHttpsFixture fixture, BrowserNavigationStage stage, Uri uri) => stage switch
    {
        BrowserNavigationStage.ResumeRequest => BrowserLoginDeadline.IntentAsync(fixture),
        BrowserNavigationStage.Authorization => BrowserLoginDeadline.ParAsync(fixture, uri),
        BrowserNavigationStage.Callback => BrowserLoginDeadline.StateAsync(fixture),
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

    /// <summary>Requires every expected request and response policy, without crediting a failure before the selected native stage.</summary>
    private static void RequirePolicies(BrowserHttpsFixture fixture, BrowserNavigationStage stage)
    {
        var intent = stage == BrowserNavigationStage.ResumeRequest;
        var callback = stage == BrowserNavigationStage.Callback;
        fixture.RequirePolicies(new(1, callback ? TwoReturns : 1, 1, intent ? 1 : TwoRedirects),
            new(TwoForms, intent ? 0 : 1, TwoForms, callback ? ThreeRedirects : TwoRedirects));
    }
    private const string Location = "location";
    private const int TwoReturns = 2;
    private const int TwoRedirects = 2;
    private const int TwoForms = 2;
    private const int ThreeRedirects = 3;
}
