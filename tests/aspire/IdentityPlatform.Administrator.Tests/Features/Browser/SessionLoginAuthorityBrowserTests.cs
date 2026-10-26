using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Rejects another genuine account and supported authority loss at the real authorization/callback browser boundaries.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserLogin")]
public sealed class SessionLoginAuthorityBrowserTests(BootstrapFixture fixture)
{
    private const string Callback = "/callback";
    private const string GovernanceLock = "AdministratorGovernanceLocks";
    private static readonly BrowserHttpsCounts ClientCounts = new(1, 2, 1, 2);
    private static readonly BrowserHttpsCounts IssuerCounts = new(2, 1, 2, 3);
    private static readonly BrowserHttpsCounts OtherAccountCounts = new(2, 1, 2, 5);

    /// <summary>Changes only the named issuer cookie before the authorization request is sent, preserving actual client correlation and original completed intent.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_GenuineCompletedIntent_When_AnotherIssuerAccountCookieIsSubstituted_Then_NoBffTicketOrExchangeOccurs(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var foreign = await BrowserSession.CreateAsync();
            var other = await BrowserOtherAccount.SignInAsync(environment, await foreign.Context.NewPageAsync());
            await using var original = await BrowserSession.CreateAsync();
            var page = await original.Context.NewPageAsync();
            await BrowserHttpsJourney.BeginAsync(environment, page);
            await BrowserHttpsJourney.PasswordAsync(environment, page);
            IResponse? response = null;
            await environment.ClientNavigation.RunAsync(BrowserNavigationStage.ResumeRedirect,
                async () => response = await CompleteAsync(environment, page), _ => page.Context.AddCookiesAsync([other.Cookie]));
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, response!.Status);
            Assert.Equal(1, environment.ParRequests);
            Assert.Equal(0, environment.TokenRequests);
            await BrowserLoginEffects.NoTicketAsync(environment);
            await BrowserOtherAccount.RequireLiveAsync(environment, other.User);
            environment.RequirePolicies(ClientCounts, OtherAccountCounts);
        });

    /// <summary>Commits a supported audited permission change after genuine code issuance but before native token exchange.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_RealBrowserCallback_When_CurrentAuthorityIsRevokedBeforeExchange_Then_NoTicketOrAutomaticRetryOccurs(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await BrowserHttpsJourney.BeginAsync(environment, page);
            await BrowserHttpsJourney.PasswordAsync(environment, page);
            IResponse? response = null;
            await environment.ClientNavigation.RunAsync(BrowserNavigationStage.Callback,
                async () => response = await CompleteAsync(environment, page), async _ =>
                {
                    await using var mutation = await NativeAuthorityMutation.PrepareAsync(environment.Issuer,
                        NativeAuthorityMutation.Change.Permissions, new NativeAuthorityRaceProbe(false, GovernanceLock));
                    await mutation.InvokeAsync();
                    await mutation.RequirePersistedAsync(environment.Issuer, NativeAuthorityMutation.Change.Permissions);
                });
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, response!.Status);
            Assert.Equal(1, environment.ParRequests);
            Assert.Equal(1, environment.TokenRequests);
            await BrowserLoginEffects.NoTicketAsync(environment);
            environment.RequirePolicies(ClientCounts, IssuerCounts);
        });

    /// <summary>Waits for the actual final callback response while the factor form and native redirects use ordinary browser networking.</summary>
    private static Task<IResponse> CompleteAsync(BrowserHttpsFixture fixture, IPage page) => page.RunAndWaitForResponseAsync(
        () => BrowserHttpsJourney.FactorAsync(fixture, page), response => response.Request.Method == HttpMethods.Get &&
            Uri.TryCreate(response.Url, UriKind.Absolute, out var uri) &&
            uri.GetLeftPart(UriPartial.Path) == new Uri(fixture.Profile.ClientUri, Callback).AbsoluteUri);
}
