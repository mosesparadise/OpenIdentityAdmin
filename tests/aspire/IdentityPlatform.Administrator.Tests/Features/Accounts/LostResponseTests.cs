using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Observes committed enrollment while the client deliberately receives neither HTTP headers nor the secret body.</summary>
[Collection("administrator-bootstrap")]
public sealed class LostResponseTests(BootstrapFixture fixture)
{
    private const string EnrollmentEvent = "administrator-enrollment";
    private const string Accepted = "accepted";
    private const string ChangedVerifiers = "A replay changed committed recovery verifier identities or values.";

    /// <summary>Dropping the committed response cannot redisplay or replace codes; the proven factor still permits a fresh login.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_UnreceivedEnrollmentResponse_When_CommitSurvivesTransportLoss_Then_ReplayCannotReplaceCodesOrRestoreCookie(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var cookies = new CookieContainer();
        using var browser = environment.Browser(cookies);
        var location = await AdministratorJourney.BeginAsync(environment, browser, enrollment: true);
        var form = await AdministratorJourney.ReadAsync(browser, location, enrollment: true);
        var code = FactorOracle.Code(form.Seed!, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await using var scope = environment.Database.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        string[] before;
        await using (var connection = new UnreceivedResponse())
        {
            await connection.SendAsync(environment.Issuer, cookies, form, code);
            await ObserveCommitAsync(database);
            before = await VerifiersAsync(database);
        }
        Assert.Equal(10, before.Length);
        Assert.Null(cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie]);
        using var other = environment.Browser(cookies, secondary: true);
        using var denied = await other.GetAsync(AdministratorJourney.Security, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        using var redisplay = await other.GetAsync(location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, redisplay.StatusCode);
        using var repeated = await AdministratorJourney.ConfirmAsync(other, form, code, enrollment: true);
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
        var after = await VerifiersAsync(database);
        Assert.True(before.SequenceEqual(after), ChangedVerifiers);
        var login = await AdministratorJourney.BeginAsync(environment, other, enrollment: false);
        var challenge = await AdministratorJourney.ReadAsync(other, login, enrollment: false);
        using var accepted = await AdministratorJourney.ConfirmAsync(other, challenge, FactorOracle.UnusedCode(form.Seed!, code), enrollment: false);
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.NotNull(cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie]);
    }

    /// <summary>Uses an independent connection and a committed audit row as the witness, never a sleep standing in for commit.</summary>
    private static async Task ObserveCommitAsync(PlatformDbContext database)
    {
        const int DeadlineSeconds = 10;
        const int PollMilliseconds = 25;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(DeadlineSeconds));
        while (!await database.Set<SecurityAuditIntent>().AnyAsync(row => row.Event == EnrollmentEvent && row.Outcome == Accepted, deadline.Token))
        {
            await Task.Delay(PollMilliseconds, deadline.Token);
        }
        Assert.Equal(1, await database.Set<AdministratorSession>().CountAsync(deadline.Token));
    }

    /// <summary>Captures the complete identity and value of recovery verifiers without putting their contents in assertion diagnostics.</summary>
    private static async Task<string[]> VerifiersAsync(PlatformDbContext database)
    {
        var rows = await database.Set<AdministratorRecoveryCode>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(TestContext.Current.CancellationToken);
        return rows.Select(row => string.Join(':', row.Id, row.UserId, row.Generation, row.Verifier, row.RedeemedAt)).ToArray();
    }
}
