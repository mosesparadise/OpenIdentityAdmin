using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Proves protected process bootstrap, actual random-key enrollment and cross-node account authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class EnrollmentTests(BootstrapFixture fixture)
{
    private const string RecoveryHeading = "Save your recovery codes";
    private const string RecoveryCodePattern = "<code>[0-9A-F]{32}</code>";
    private const string ActiveState = "Active";
    private const string PasswordAndOtp = "pwd otp";
    private const string EnrollmentEvent = "administrator-enrollment";
    private const string AcceptedOutcome = "accepted";
    private const int RecoveryCount = 10;
    private const int AbsoluteSeconds = 28800;

    /// <summary>Only committed native enrollment creates an enabled MFA account, ten one-time codes and a reusable stored session.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_PendingBootstrap_When_RandomAuthenticatorIsConfirmed_Then_CommittedSessionWorksAcrossNodes(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var cookies = new CookieContainer();
        using var browser = environment.Browser(cookies);
        var location = await AdministratorJourney.BeginAsync(environment, browser, enrollment: true);
        Assert.True(cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie] is null);
        var form = await AdministratorJourney.ReadAsync(browser, location, enrollment: true);
        var code = FactorOracle.Code(form.Seed!, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        using var result = await AdministratorJourney.ConfirmAsync(browser, form, code, enrollment: true);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie]);
        var html = await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(html.Contains(RecoveryHeading, StringComparison.Ordinal));
        Assert.True(System.Text.RegularExpressions.Regex.Count(html, RecoveryCodePattern) == RecoveryCount);
        await AssertCommittedAsync(environment);
        using var other = environment.Browser(cookies, secondary: true);
        using var status = await other.GetAsync(AdministratorJourney.Security, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        using var redisplay = await browser.GetAsync(location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, redisplay.StatusCode);
        using var replay = await AdministratorJourney.ConfirmAsync(browser, form, code, enrollment: true);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        await AssertCommittedAsync(environment);
    }

    /// <summary>Inspects independently persisted activation, protected factors, replay, actual proof times and permanent receipt.</summary>
    private static async Task AssertCommittedAsync(AdministratorEnvironment environment)
    {
        await using var scope = environment.Database.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var user = await database.Users.SingleAsync(token);
        var enrollment = await database.Set<AdministratorEnrollment>().SingleAsync(token);
        var session = await database.Set<AdministratorSession>().SingleAsync(token);
        var factor = await database.Set<AdministratorFactor>().SingleAsync(token);
        var replay = await database.Set<AdministratorReplay>().SingleAsync(token);
        Assert.True(user.IsEnabled && user.TwoFactorEnabled && enrollment.State == ActiveState);
        Assert.True(session.AuthenticationMethods == PasswordAndOtp && session.SecurityStamp == user.SecurityStamp);
        Assert.True(session.PasswordVerifiedAt <= session.FactorVerifiedAt && session.FactorVerifiedAt <= session.CreatedAt);
        Assert.Equal(AbsoluteSeconds, session.ExpiresAt - session.CreatedAt);
        Assert.Equal(enrollment.FactorGeneration, factor.Generation);
        Assert.Equal(factor.Generation, replay.Generation);
        Assert.Equal(RecoveryCount, await database.Set<AdministratorRecoveryCode>().CountAsync(token));
        Assert.Equal(1, await database.Set<AdministratorBootstrapReceipt>().CountAsync(token));
        Assert.Equal(1, await database.Set<SecurityAuditIntent>().CountAsync(row => row.Event == EnrollmentEvent && row.Outcome == AcceptedOutcome, token));
    }
}
