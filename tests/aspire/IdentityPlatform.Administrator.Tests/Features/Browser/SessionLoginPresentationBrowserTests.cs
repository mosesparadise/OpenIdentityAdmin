using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Exercises both themes and real keyboard error/retry paths through password, MFA and restricted recovery forms.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class SessionLoginPresentationBrowserTests(BootstrapFixture fixture)
{
    private static readonly bool[] Themes = [false, true];
    private const string ProviderTrait = "Provider";
    private const string Username = "#username";
    private const string Password = "#password";
    private const string Code = "#code";
    private const string WrongPassword = "synthetic-incorrect-password";
    private const string RecoveryForm = "form[action='/account/administrator/recovery/verify']";
    private const string ErrorPath = "/bff/login/error";

    /// <summary>Discovers both explicit palettes independently on each mandatory relational provider.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Cases =>
        from provider in IssuerContinuationProviderFixture.Providers
        from dark in Themes
        select new TheoryDataRow<string, bool>(provider, dark).WithTrait(ProviderTrait, provider);

    /// <summary>Rejected credentials remain generic, cleared and associated with focused errors; keyboard correction completes genuine native authentication.</summary>
    [Theory, MemberData(nameof(Cases))]
    public Task Given_ThemedProductForms_When_PasswordAndMfaAreRejectedThenCorrected_Then_AccessibleKeyboardRetryCompletes(string provider, bool dark) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await BrowserHttpsJourney.BeginAsync(environment, page);
            await BrowserLoginPresentation.PageAsync(page, dark);
            await DeniedAsync(page, () => AdministratorKeyboard.PasswordAsync(page, environment.Issuer.Account.Journey.Lease.Username, WrongPassword));
            await BrowserLoginPresentation.ErrorAsync(page, Username, Password);
            await BrowserLoginPresentation.PageAsync(page, dark);
            await BrowserLoginEffects.NoTicketAsync(environment);
            await AdministratorKeyboard.PasswordAsync(page, environment.Issuer.Account.Journey.Lease.Username, environment.Issuer.Account.Password);
            await BrowserLoginPresentation.PageAsync(page, dark);
            var invalid = FactorOracle.InvalidCode(environment.Issuer.Account.Journey.Seed);
            var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await DeniedAsync(page, () => BrowserLoginPresentation.FactorAsync(page, invalid));
            FactorOracle.RequireWindow(environment.Issuer.Account.Journey.Seed, invalid, started, valid: false);
            await BrowserLoginPresentation.ErrorAsync(page, Code);
            await BrowserLoginPresentation.PageAsync(page, dark);
            await BrowserLoginEffects.NoTicketAsync(environment);
            await BrowserLoginPresentation.FactorAsync(page, await IssuerContinuationProviderFixture.UnusedCodeAsync(environment.Issuer.Account.Journey));
            await BrowserHttpsJourney.FinishAsync(environment, page);
            Assert.Equal(1, environment.TokenRequests);
            await page.GotoAsync(new Uri(environment.Profile.ClientUri, ErrorPath).AbsoluteUri);
            await BrowserLogoutPresentation.InitializeAsync(page, dark, initialDark: false);
            await BrowserLogoutPresentation.RequireAsync(page, dark);
        });

    /// <summary>Recovery password/code failures remain generic and keyboard reachable, without granting replacement or product authority.</summary>
    [Theory, MemberData(nameof(Cases))]
    public Task Given_ThemedRecoveryForms_When_PasswordAndCodeAreRejected_Then_FocusedSafeErrorsAndRestartRemainAvailable(string provider, bool dark) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await page.GotoAsync(new Uri(environment.Profile.Issuer, SecurityHttp.Recovery).AbsoluteUri);
            await BrowserLoginPresentation.PageAsync(page, dark);
            await DeniedAsync(page, () => AdministratorKeyboard.PasswordAsync(page, environment.Issuer.Account.Journey.Lease.Username, WrongPassword));
            await BrowserLoginPresentation.ErrorAsync(page, Username, Password);
            await BrowserLoginPresentation.PageAsync(page, dark);
            await AdministratorKeyboard.PasswordAsync(page, environment.Issuer.Account.Journey.Lease.Username, environment.Issuer.Account.Password);
            await BrowserLoginPresentation.PageAsync(page, dark);
            var invalid = new string(InvalidRecoveryCharacter, RecoveryLength);
            var issued = environment.Issuer.Account.Grant.RecoveryCodes.Contains(invalid, StringComparer.Ordinal);
            Assert.False(issued);
            await DeniedAsync(page, () => BrowserLoginPresentation.RecoveryAsync(page, invalid));
            await BrowserLoginPresentation.ErrorAsync(page);
            await BrowserLoginPresentation.PageAsync(page, dark);
            Assert.Equal(0, await page.Locator(RecoveryForm).CountAsync());
            await BrowserLoginPresentation.RestartAsync(page);
            Assert.True(page.Url == new Uri(environment.Profile.Issuer, AdministratorJourney.Login).AbsoluteUri);
            await BrowserLoginEffects.NoTicketAsync(environment);
            Assert.Equal(0, environment.ParRequests);
            Assert.Equal(0, environment.TokenRequests);
        });

    /// <summary>Checks the real native POST response rather than inferring rejection from rendered text alone.</summary>
    private static async Task DeniedAsync(IPage page, Func<Task> submit)
    {
        var response = await page.RunAndWaitForResponseAsync(submit, item => item.Request.Method == HttpMethods.Post);
        Assert.Equal(StatusCodes.Status400BadRequest, response.Status);
    }
    private const char InvalidRecoveryCharacter = '0';
    private const int RecoveryLength = 32;
}
