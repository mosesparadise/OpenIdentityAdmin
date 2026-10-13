using System.Net;
using System.Text.Json;
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
    private const string Denied = "denied";
    private const string Consumed = "Consumed";
    private const int Contenders = 2;

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
        var sessions = await database.Set<AdministratorSession>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken);
        var interactions = await database.Set<AdministratorInteraction>().AsNoTracking()
            .Where(row => row.PublicId == firstForm.Interaction || row.PublicId == secondForm.Interaction).ToArrayAsync(TestContext.Current.CancellationToken);
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var code = FactorOracle.UnusedCode(setup.Seed, setup.Code);
        var responses = await JourneyContention.CompleteAsync(environment, provider, first, second, firstForm, secondForm, code, enrollment: false);
        try
        {
            FactorOracle.RequireWindow(setup.Seed, code, started, valid: true);
            var winner = RequireResponses(responses, [firstCookies, secondCookies], environment.Issuer);
            await RequireIsolationAsync(database, sessions, interactions, winner == 0 ? firstForm.Interaction : secondForm.Interaction);
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
        var after = await CountsAsync(database);
        Assert.Equal((before.Sessions + 1, before.Replays + 1, before.Successes + 1, before.Denials + 1), after);
    }

    /// <summary>Snapshots independent committed session/replay evidence and counts successful and legitimate denied factor audits separately.</summary>
    private static async Task<(int Sessions, int Replays, int Successes, int Denials)> CountsAsync(PlatformDbContext database)
    {
        var token = TestContext.Current.CancellationToken;
        return (await database.Set<AdministratorSession>().CountAsync(token), await database.Set<AdministratorReplay>().CountAsync(token),
            await database.Set<SecurityAuditIntent>().CountAsync(row => row.Event == FactorEvent && row.Outcome == Accepted, token),
            await database.Set<SecurityAuditIntent>().CountAsync(row => row.Event == FactorEvent && row.Outcome == Denied, token));
    }

    /// <summary>Requires exactly one successful response and cookie, identifying the winner independently of request order.</summary>
    private static int RequireResponses(HttpResponseMessage[] responses, CookieContainer[] cookies, Uri issuer)
    {
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Redirect));
        Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest));
        for (var index = 0; index < responses.Length; index++)
        {
            Assert.Equal(responses[index].StatusCode == HttpStatusCode.Redirect,
                cookies[index].GetCookies(issuer)[AdministratorJourney.SessionCookie] is not null);
        }
        return Array.FindIndex(responses, response => response.StatusCode == HttpStatusCode.Redirect);
    }

    /// <summary>Preserves every predecessor session and every interaction binding; the loser gains only its legitimate failed-attempt metadata.</summary>
    private static async Task RequireIsolationAsync(PlatformDbContext database, AdministratorSession[] sessions,
        AdministratorInteraction[] interactions, string winner)
    {
        var token = TestContext.Current.CancellationToken;
        Assert.Single(sessions);
        Assert.Equal(Contenders, interactions.Length);
        Assert.Equal(Contenders, interactions.Select(row => row.PublicId).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(interactions, row => row.PublicId == winner);
        foreach (var original in sessions)
        {
            var current = await database.Set<AdministratorSession>().AsNoTracking().SingleAsync(row => row.Id == original.Id, token);
            Assert.True(JsonSerializer.Serialize(original) == JsonSerializer.Serialize(current));
        }
        foreach (var original in interactions)
        {
            var current = await database.Set<AdministratorInteraction>().AsNoTracking().SingleAsync(row => row.Id == original.Id, token);
            if (original.PublicId == winner)
            {
                original.Revision++;
                original.State = Consumed;
            }
            else
            {
                original.Attempts++;
            }
            Assert.True(JsonSerializer.Serialize(original) == JsonSerializer.Serialize(current));
        }
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
