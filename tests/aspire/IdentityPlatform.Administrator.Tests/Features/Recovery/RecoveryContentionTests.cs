using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Recovery;

/// <summary>Observes actual two-node lock contention before asserting single-winner recovery and predecessor invalidation.</summary>
[Collection("administrator-bootstrap")]
public sealed class RecoveryContentionTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private const string GovernanceStage = "%UPDATE%AdministratorGovernance%";
    private const string RecoveryEvent = "administrator-recovery";
    private const string Accepted = "accepted";
    private static readonly bool[] CodeModes = [false, true];
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];

    /// <summary>Runs shared-code and distinct-code contention independently on each production database provider.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Cases => from provider in Providers
                                                                    from sameCode in CodeModes
                                                                    select new TheoryDataRow<string, bool>(provider, sameCode).WithTrait(ProviderTrait, provider);

    /// <summary>Only one preadmitted interaction may enter recovery; another code requires a new password-bound admission after the winner commits.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_IndependentRecoveryInteractions_When_TwoNodesContend_Then_OneRestrictedTransitionAndNoFullSessionCommit(string provider, bool sameCode)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        using var setup = environment.Browser(new CookieContainer());
        var enrolled = await SecurityHttp.EnrollAsync(environment, setup);
        var firstCookies = new CookieContainer();
        var secondCookies = new CookieContainer();
        using var first = environment.Browser(firstCookies);
        using var second = environment.Browser(secondCookies, secondary: true);
        var firstForm = await AdministratorJourney.ReadAsync(first, await AdministratorJourney.BeginAtAsync(environment, first, SecurityHttp.Recovery), enrollment: false);
        var secondForm = await AdministratorJourney.ReadAsync(second, await AdministratorJourney.BeginAtAsync(environment, second, SecurityHttp.Recovery), enrollment: false);
        await using var scope = environment.Database.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var before = await SecuritySnapshot.RecoveryAsync(database);
        var codes = new[] { enrolled.RecoveryCodes[0], enrolled.RecoveryCodes[sameCode ? 0 : 1] };
        var responses = await JourneyContention.RunAsync(environment, provider, first, second, new(true, false, [], []), GovernanceStage,
            () => Task.WhenAll(AdministratorJourney.ConfirmAtAsync(first, firstForm, codes[0], SecurityHttp.RecoveryVerify),
                AdministratorJourney.ConfirmAtAsync(second, secondForm, codes[1], SecurityHttp.RecoveryVerify)));
        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Redirect));
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest));
            var winner = Array.FindIndex(responses, response => response.StatusCode == HttpStatusCode.Redirect);
            SecuritySnapshot.RequireSingleClaim(before, await SecuritySnapshot.RecoveryAsync(database), codes[winner]);
            await RequireWinnerAsync(environment, database, winner == 0 ? first : second, responses[winner].Headers.Location!, enrolled, codes[winner]);
            Assert.True(firstCookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie] is null);
            Assert.True(secondCookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie] is null);
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
    }

    /// <summary>Requires single-winner evidence, then uses another code only through fresh admission and verifies old staging becomes unavailable.</summary>
    private static async Task RequireWinnerAsync(AdministratorEnvironment environment, PlatformDbContext database, HttpClient winner,
        Uri replacement, SecurityEnrollment enrolled, string spent)
    {
        var token = TestContext.Current.CancellationToken;
        Assert.Equal(1, await database.Set<AdministratorSession>().CountAsync(token));
        Assert.False(await database.Set<AdministratorSession>().AnyAsync(row => !row.Revoked, token));
        Assert.Equal(1, await database.Set<SecurityAuditIntent>().CountAsync(row => row.Event == RecoveryEvent && row.Outcome == Accepted, token));
        Assert.NotNull((await AdministratorJourney.ReadAsync(winner, replacement, enrollment: true)).Seed);
        var next = await AdministratorJourney.BeginAtAsync(environment, winner, SecurityHttp.Recovery);
        var form = await AdministratorJourney.ReadAsync(winner, next, enrollment: false);
        var another = enrolled.RecoveryCodes.First(code => code != spent);
        var renewed = await SecurityHttp.TransitionAsync(winner, form, another, recovery: true);
        using var old = await winner.GetAsync(replacement, token);
        Assert.Equal(HttpStatusCode.BadRequest, old.StatusCode);
        Assert.NotNull((await AdministratorJourney.ReadAsync(winner, renewed, enrollment: true)).Seed);
        const int ClaimsAfterFreshAdmission = 2;
        Assert.Equal(ClaimsAfterFreshAdmission, await database.Set<AdministratorRecoveryCode>().CountAsync(row => row.RedeemedAt != null, token));
    }
}
