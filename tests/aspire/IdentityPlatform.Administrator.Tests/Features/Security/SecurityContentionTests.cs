using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Security;

/// <summary>Exercises current-session revocation racing a real native regeneration on independent AuthServer nodes.</summary>
[Collection("administrator-bootstrap")]
public sealed class SecurityContentionTests(BootstrapFixture fixture)
{
    private const string AccountStage = "%UPDATE%AdministratorAuthentication%";
    private const string RegenerationEvent = "administrator-regeneration";
    private const string LogoutEvent = "administrator-logout";
    private const string Accepted = "accepted";

    /// <summary>Two observed waiters cannot both use the same original session for logout and a credential-changing action.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_SessionBoundRegeneration_When_OtherNodeLogsOutConcurrently_Then_ExactlyOneActionCanCommit(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var cookies = new CookieContainer();
        using var first = environment.Browser(cookies);
        var enrolled = await SecurityHttp.EnrollAsync(environment, first);
        var location = await AdministratorJourney.BeginAtAsync(environment, first, SecurityHttp.Regeneration);
        var form = await AdministratorJourney.ReadAsync(first, location, enrollment: false);
        var secondCookies = new CookieContainer();
        foreach (Cookie cookie in cookies.GetCookies(environment.Issuer))
        {
            secondCookies.Add(environment.Issuer, new Cookie(cookie.Name, cookie.Value, cookie.Path));
        }
        using var second = environment.Browser(secondCookies, secondary: true);
        using var logoutPage = await second.GetAsync(SecurityHttp.Logout, TestContext.Current.CancellationToken);
        var csrf = AdministratorJourney.Input(await logoutPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), AdministratorJourney.Antiforgery);
        await using var scope = environment.Database.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var userId = await database.Users.Select(row => row.Id).SingleAsync(TestContext.Current.CancellationToken);
        var code = FactorOracle.UnusedCode(enrolled.Seed, enrolled.AcceptedCode);
        var responses = await JourneyContention.RunAsync(environment, provider, first, second, new(false, false, [], [userId]), AccountStage,
            () => Task.WhenAll(AdministratorJourney.ConfirmAtAsync(first, form, code, SecurityHttp.Regenerate),
                AdministratorJourney.PostAsync(second, SecurityHttp.Logout, new Dictionary<string, string> { [AdministratorJourney.Antiforgery] = csrf })));
        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect));
            Assert.Equal(1, responses.Count(response => response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized));
            var successes = await database.Set<SecurityAuditIntent>().CountAsync(row =>
                (row.Event == RegenerationEvent || row.Event == LogoutEvent) && row.Outcome == Accepted, TestContext.Current.CancellationToken);
            Assert.Equal(1, successes);
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
    }
}
