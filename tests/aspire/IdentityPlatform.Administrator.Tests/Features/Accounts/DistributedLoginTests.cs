using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Proves that independently admitted real-node logins share one accepted-factor ledger.</summary>
[Collection("administrator-bootstrap")]
public sealed class DistributedLoginTests(BootstrapFixture fixture)
{
    private const string FactorEvent = "administrator-factor";
    private const string Accepted = "accepted";

    /// <summary>Distinct browser-bound interactions cannot both consume the same unused native-valid TOTP.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_IndependentLoginInteractions_When_TwoNodesContendForSameOtp_Then_OnlyOneNewSessionAndProofCommit(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var setup = await EnrollAsync(environment);
        var firstCookies = new CookieContainer();
        var secondCookies = new CookieContainer();
        using var first = environment.Browser(firstCookies);
        using var second = environment.Browser(secondCookies, secondary: true);
        var firstLocation = await AdministratorJourney.BeginAsync(environment, first, enrollment: false);
        var secondLocation = await AdministratorJourney.BeginAsync(environment, second, enrollment: false);
        var firstForm = await AdministratorJourney.ReadAsync(first, firstLocation, enrollment: false);
        var secondForm = await AdministratorJourney.ReadAsync(second, secondLocation, enrollment: false);
        Assert.True(firstForm.Interaction != secondForm.Interaction);
        await using var scope = environment.Database.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var before = await CountsAsync(database);
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var code = FactorOracle.UnusedCode(setup.Seed, setup.Code);
        var responses = await JourneyContention.CompleteAsync(environment, provider, first, second, firstForm, secondForm, code, enrollment: false);
        try
        {
            FactorOracle.RequireWindow(setup.Seed, code, started, valid: true);
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Redirect));
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest));
            CookieContainer[] cookies = [firstCookies, secondCookies];
            for (var index = 0; index < responses.Length; index++)
            {
                Assert.Equal(responses[index].StatusCode == HttpStatusCode.Redirect,
                    cookies[index].GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie] is not null);
            }
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
        var after = await CountsAsync(database);
        Assert.Equal((before.Sessions + 1, before.Replays + 1, before.Successes + 1), after);
    }

    /// <summary>Snapshots independent committed session, replay and successful-factor evidence, excluding legitimate denial audits.</summary>
    private static async Task<(int Sessions, int Replays, int Successes)> CountsAsync(PlatformDbContext database)
    {
        var token = TestContext.Current.CancellationToken;
        return (await database.Set<AdministratorSession>().CountAsync(token), await database.Set<AdministratorReplay>().CountAsync(token),
            await database.Set<SecurityAuditIntent>().CountAsync(row => row.Event == FactorEvent && row.Outcome == Accepted, token));
    }

    /// <summary>Establishes a real protected account before creating independent login browsers.</summary>
    private static async Task<(string Seed, string Code)> EnrollAsync(AdministratorEnvironment environment)
    {
        using var browser = environment.Browser(new CookieContainer());
        var location = await AdministratorJourney.BeginAsync(environment, browser, enrollment: true);
        var form = await AdministratorJourney.ReadAsync(browser, location, enrollment: true);
        var code = FactorOracle.Code(form.Seed!, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        using var response = await AdministratorJourney.ConfirmAsync(browser, form, code, enrollment: true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (form.Seed!, code);
    }
}
