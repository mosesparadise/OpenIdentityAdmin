using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Checks the durable failure budget across real nodes, correct passwords and entirely new browser cookie jars.</summary>
[Collection("administrator-bootstrap")]
public sealed class DistributedBudgetTests(BootstrapFixture fixture)
{
    private const int FailureLimit = 5;
    private const int NodeCount = 2;
    private const string Username = "Username";
    private const string Password = "Password";
    private const string Unknown = "unknown-synthetic-administrator";
    private const string GenericError = "Unable to complete this request. Check your details or start again.";

    /// <summary>Correct passwords never reset factor failures; the fifth failure locks both nodes and unknown/locked accounts share the fixed denial.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_FactorFailuresAcrossNodes_When_NewBrowsersSubmitCorrectPasswords_Then_FifthFailureLocksWithoutNewAuthority(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        using var owner = environment.Browser(new CookieContainer());
        var enrollment = await SecurityHttp.EnrollAsync(environment, owner);
        await using var scope = environment.Database.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await RequireDisabledDenialAsync(environment, database);
        for (var failure = 1; failure <= FailureLimit; failure++)
        {
            var cookies = new CookieContainer();
            using var browser = environment.Browser(cookies, secondary: failure % NodeCount == 0);
            var location = await AdministratorJourney.BeginAsync(environment, browser, enrollment: false);
            var form = await AdministratorJourney.ReadAsync(browser, location, enrollment: false);
            using var denied = await AdministratorJourney.ConfirmAsync(browser, form, FactorOracle.InvalidCode(enrollment.Seed), enrollment: false);
            await RequireGenericAsync(denied);
            Assert.True(cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie] is null);
            Assert.Equal(failure, (await database.Set<AdministratorAuthentication>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).Failures);
        }
        using var primary = environment.Browser(new CookieContainer());
        using var secondary = environment.Browser(new CookieContainer(), secondary: true);
        await RequirePasswordDeniedAsync(environment, primary, environment.Database.Username);
        await RequirePasswordDeniedAsync(environment, secondary, environment.Database.Username);
        await RequirePasswordDeniedAsync(environment, primary, Unknown);
        Assert.Equal(1, await database.Set<AdministratorSession>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await database.Set<AdministratorReplay>().CountAsync(TestContext.Current.CancellationToken));
        Assert.True((await database.Users.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).LockoutEnd > DateTimeOffset.UtcNow);
    }

    /// <summary>Isolates disabled-account denial before any lockout exists, then restores enablement for the genuine successful admissions.</summary>
    private static async Task RequireDisabledDenialAsync(AdministratorEnvironment environment, PlatformDbContext database)
    {
        var token = TestContext.Current.CancellationToken;
        Assert.Null((await database.Users.AsNoTracking().SingleAsync(token)).LockoutEnd);
        await database.Users.ExecuteUpdateAsync(update => update.SetProperty(row => row.IsEnabled, false), token);
        using var browser = environment.Browser(new CookieContainer(), secondary: true);
        await RequirePasswordDeniedAsync(environment, browser, environment.Database.Username);
        await database.Users.ExecuteUpdateAsync(update => update.SetProperty(row => row.IsEnabled, true), token);
    }

    /// <summary>Uses valid native antiforgery and an exact password form so denial cannot be satisfied by malformed input.</summary>
    private static async Task RequirePasswordDeniedAsync(AdministratorEnvironment environment, HttpClient browser, string username)
    {
        using var page = await browser.GetAsync(AdministratorJourney.Login, TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var denied = await AdministratorJourney.PostAsync(browser, AdministratorJourney.Login, new Dictionary<string, string>
        {
            [AdministratorJourney.Antiforgery] = AdministratorJourney.Input(html, AdministratorJourney.Antiforgery),
            [Username] = username,
            [Password] = environment.Database.Password
        });
        await RequireGenericAsync(denied);
    }

    /// <summary>Compares a fixed public error classification, never secret-bearing HTML or timing claims.</summary>
    private static async Task RequireGenericAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(html.Contains(GenericError, StringComparison.Ordinal));
    }
}
