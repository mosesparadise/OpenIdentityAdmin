using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Exercises genuine pilot and restricted-recovery cookies through actual browser forms before product sign-in.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserLogin")]
public sealed class SessionLoginIsolationBrowserTests(BootstrapFixture fixture)
{
    private const string PilotUsername = "synthetic-browser-pilot";
    private const string PilotCookie = "__Host-identity-pilot";
    private const string AdministratorCookie = "__Host-identity-administrator";
    private const string Username = "#username";
    private const string Password = "#password";
    private const string Submit = "#submit";
    private const string RecoveryCode = "#recovery-code";
    private const string Replacement = "#setup-key";
    private const string RecoveryRequired = "RecoveryRequired";
    private const string AnyQuery = "**";
    private const string BeginRecovery = "Begin authenticator recovery";

    /// <summary>A real pilot authorization/password/callback leaves BFF anonymous and never substitutes for fresh administrator password/MFA.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_RealPilotBrowserSession_When_ProductLoginStarts_Then_FreshAdministratorCeremonyIsStillRequired(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var pilot = await BrowserPilotClient.CreateAsync();
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, pilot: pilot);
            pilot.Client.Issuer = environment.Profile.Issuer;
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            var flow = pilot.Client.Prepare();
            await page.GotoAsync(pilot.Client.StartUri(flow).AbsoluteUri);
            await page.Locator(Username).FillAsync(PilotUsername);
            await page.Locator(Password).FillAsync(environment.Issuer.Account.Password);
            await page.Locator(Submit).ClickAsync();
            await page.WaitForURLAsync(pilot.Client.Callback + AnyQuery);
            Assert.True(await flow.Completion.Task.WaitAsync(TimeSpan.FromSeconds(CompletionSeconds), TestContext.Current.CancellationToken));
            Assert.Equal(1, flow.CallbackCount);
            Assert.Equal(0, flow.ExchangeCount);
            var cookies = await session.Context.CookiesAsync();
            Assert.Equal(1, cookies.Count(cookie => cookie.Name == PilotCookie));
            Assert.Equal(0, cookies.Count(cookie => cookie.Name == AdministratorCookie));
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, false);
            await BrowserLoginEffects.NoTicketAsync(environment);
            Assert.Equal(0, environment.ParRequests);
            await BrowserHttpsJourney.SignInAsync(environment, page);
            Assert.Equal(1, environment.ParRequests);
            Assert.Equal(1, environment.TokenRequests);
        });

    /// <summary>Genuine recovery consumes a real code and grants only replacement authority; B3 password admission then denies without native exchange.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_RestrictedRecoveryInRealBrowser_When_ProductLoginIsAttempted_Then_NoAdministratorTicketIsIssued(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await page.GotoAsync(new Uri(environment.Profile.Issuer, SecurityHttp.Recovery).AbsoluteUri);
            await BrowserHttpsJourney.PasswordAsync(environment, page);
            await page.Locator(RecoveryCode).FillAsync(environment.Issuer.Account.Grant.RecoveryCodes[0]);
            await page.GetByRole(AriaRole.Button, new()
            {
                Name = BeginRecovery,
                Exact = true
            }).ClickAsync();
            await page.Locator(Replacement).WaitForAsync();
            await using var scope = environment.Issuer.Account.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            Assert.Equal(RecoveryRequired, await database.Set<AdministratorEnrollment>().Select(row => row.State).SingleAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, (await session.Context.CookiesAsync()).Count(cookie => cookie.Name == AdministratorCookie));
            await BrowserHttpsJourney.BeginAsync(environment, page);
            var denied = await page.RunAndWaitForResponseAsync(() => BrowserHttpsJourney.PasswordAsync(environment, page),
                response => response.Request.Method == HttpMethods.Post && new Uri(response.Url).AbsolutePath == LoginPath);
            Assert.Equal(StatusCodes.Status400BadRequest, denied.Status);
            Assert.Equal(0, environment.ParRequests);
            Assert.Equal(0, environment.TokenRequests);
            await BrowserLoginEffects.NoTicketAsync(environment);
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, false);
        });
    private const string LoginPath = "/account/administrator/browser/login";
    private const int CompletionSeconds = 15;
}
