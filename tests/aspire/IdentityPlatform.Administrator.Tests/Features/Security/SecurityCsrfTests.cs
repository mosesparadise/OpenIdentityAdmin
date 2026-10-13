using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Security;

/// <summary>Exercises real antiforgery filtering on every new account-security POST with an actually authorized browser.</summary>
[Collection("administrator-bootstrap")]
public sealed class SecurityCsrfTests(BootstrapFixture fixture)
{
    private const string InvalidToken = "invalid-but-nonempty-antiforgery-token";
    private const string Username = "Username";
    private const string Password = "Password";

    /// <summary>Each otherwise-valid action first rejects a corrupt token, then succeeds with the genuine token and identical native proof.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CurrentAdministrator_When_EachSecurityPostHasCorruptCsrf_Then_ValidProofRemainsUsableOnlyWithGenuineToken(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        using var browser = environment.Browser(new CookieContainer());
        var enrolled = await SecurityHttp.EnrollAsync(environment, browser);
        await using var scope = environment.Database.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var replacement = await BeginAsync(environment, browser, database, SecurityHttp.Replacement);
        var code = FactorOracle.UnusedCode(enrolled.Seed, enrolled.AcceptedCode);
        await RejectCodeAsync(browser, database, replacement, code, SecurityHttp.Replace);
        var location = await SecurityHttp.TransitionAsync(browser, replacement, code, recovery: false);
        var staged = await AdministratorJourney.ReadAsync(browser, location, enrollment: true);
        var installed = FactorOracle.Code(staged.Seed!, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await RejectCodeAsync(browser, database, staged, installed, SecurityHttp.Confirm);
        using var completed = await AdministratorJourney.ConfirmAtAsync(browser, staged, installed, SecurityHttp.Confirm);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var regeneration = await BeginAsync(environment, browser, database, SecurityHttp.Regeneration);
        var next = FactorOracle.UnusedCode(staged.Seed!, installed);
        await RejectCodeAsync(browser, database, regeneration, next, SecurityHttp.Regenerate);
        using var regenerated = await AdministratorJourney.ConfirmAtAsync(browser, regeneration, next, SecurityHttp.Regenerate);
        Assert.Equal(HttpStatusCode.OK, regenerated.StatusCode);
        var codes = await SecurityHttp.CodesAsync(regenerated);
        var recovery = await BeginAsync(environment, browser, database, SecurityHttp.Recovery);
        await RejectCodeAsync(browser, database, recovery, codes[0], SecurityHttp.RecoveryVerify);
        location = await SecurityHttp.TransitionAsync(browser, recovery, codes[0], recovery: true);
        staged = await AdministratorJourney.ReadAsync(browser, location, enrollment: true);
        using var restored = await AdministratorJourney.ConfirmAtAsync(browser, staged,
            FactorOracle.Code(staged.Seed!, DateTimeOffset.UtcNow.ToUnixTimeSeconds()), SecurityHttp.Confirm);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        await LogoutAsync(browser, database);
    }

    /// <summary>Tests cryptographic rejection on a complete correct-password form, then admits the identical credentials normally.</summary>
    private static async Task<AdministratorForm> BeginAsync(AdministratorEnvironment environment, HttpClient browser, PlatformDbContext database, string path)
    {
        using var page = await browser.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var fields = new Dictionary<string, string>
        {
            [AdministratorJourney.Antiforgery] = InvalidToken,
            [Username] = environment.Database.Username,
            [Password] = environment.Database.Password
        };
        await RejectAsync(browser, database, path, fields);
        var location = await AdministratorJourney.BeginAtAsync(environment, browser, path);
        return await AdministratorJourney.ReadAsync(browser, location, enrollment: false);
    }

    /// <summary>Corrupts only cryptographic antiforgery while retaining valid shape, locator, cookie and unused native code.</summary>
    private static Task RejectCodeAsync(HttpClient browser, PlatformDbContext database, AdministratorForm form, string code, string path) =>
        RejectAsync(browser, database, path, new Dictionary<string, string>
        {
            [AdministratorJourney.Antiforgery] = InvalidToken,
            [AdministratorJourney.Interaction] = form.Interaction,
            [AdministratorJourney.CodeField] = code
        });

    /// <summary>Requires no security writes on rejected CSRF, without confusing ordinary cookie activity updates with mutation.</summary>
    private static async Task RejectAsync(HttpClient browser, PlatformDbContext database, string path, Dictionary<string, string> fields)
    {
        var before = await AuthorityAsync(database);
        using var denied = await AdministratorJourney.PostAsync(browser, path, fields);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Equal(before, await AuthorityAsync(database));
    }

    /// <summary>Tests logout with exact form shape and corrupted token, followed by the actual accepted token.</summary>
    private static async Task LogoutAsync(HttpClient browser, PlatformDbContext database)
    {
        using var page = await browser.GetAsync(SecurityHttp.Logout, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var csrf = AdministratorJourney.Input(await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), AdministratorJourney.Antiforgery);
        await RejectAsync(browser, database, SecurityHttp.Logout, new Dictionary<string, string> { [AdministratorJourney.Antiforgery] = InvalidToken });
        using var accepted = await AdministratorJourney.PostAsync(browser, SecurityHttp.Logout,
            new Dictionary<string, string> { [AdministratorJourney.Antiforgery] = csrf });
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
    }

    /// <summary>Hashes credential and authority fields while excluding legitimate validator activity/watermark updates.</summary>
    private static async Task<string> AuthorityAsync(PlatformDbContext database)
    {
        var token = TestContext.Current.CancellationToken;
        var state = new
        {
            Factors = await database.Set<AdministratorFactor>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Recovery = await database.Set<AdministratorRecoveryCode>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Interactions = await database.Set<AdministratorInteraction>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Sessions = await database.Set<AdministratorSession>().AsNoTracking().OrderBy(row => row.Id)
                .Select(row => new { row.Id, row.PublicId, row.Revoked, row.Generation, row.FactorGeneration, row.SecurityStamp }).ToArrayAsync(token),
            Audit = await database.Set<SecurityAuditIntent>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token)
        };
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(state)));
    }
}
