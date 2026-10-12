using System.Net;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Exercises actual Razor response ordering, distributed proof consumption and every-request cookie authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class HttpAuthorityTests(BootstrapFixture fixture)
{
    private const string Accepted = "accepted";
    private const string EnrollmentEvent = "administrator-enrollment";
    private const string RecoveryHeading = "Save your recovery codes";
    private static readonly string[] UnavailableSurfaces = ["/api/v1/clients", "/docs/scalar", "/openapi/v1.json", "/connect/authorize", "/connect/token"];

    /// <summary>Ordinary native login creates a minimal stored session; revocation immediately denies its genuine cookie on the other node.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_EnrolledAccount_When_PasswordAndNewOtpAuthenticate_Then_OnlyCurrentAccountSessionIsAuthorized(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var setup = await EnrollAsync(environment);
        var cookies = new CookieContainer();
        using var browser = environment.Browser(cookies);
        var continuation = await AdministratorJourney.BeginAsync(environment, browser, enrollment: false);
        using var passwordOnly = await browser.GetAsync(AdministratorJourney.Security, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, passwordOnly.StatusCode);
        Assert.Null(cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie]);
        var form = await AdministratorJourney.ReadAsync(browser, continuation, enrollment: false);
        using var completion = await AdministratorJourney.ConfirmAsync(browser, form, FactorOracle.UnusedCode(setup.Seed!, setup.Code), enrollment: false);
        Assert.Equal(HttpStatusCode.Redirect, completion.StatusCode);
        Assert.Equal(AdministratorJourney.Security, completion.Headers.Location?.OriginalString);
        Assert.NotNull(cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie]);
        using var other = environment.Browser(cookies, secondary: true);
        using var status = await other.GetAsync(AdministratorJourney.Security, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        foreach (var path in UnavailableSurfaces)
        {
            using var response = await other.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        await using var scope = environment.Database.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await database.Set<AdministratorSession>().ExecuteUpdateAsync(update => update.SetProperty(row => row.Revoked, true), TestContext.Current.CancellationToken);
        using var revoked = await other.GetAsync(AdministratorJourney.Security, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
    }

    /// <summary>A real audit permission failure cannot emit a session or recovery response; restored permission permits the original proof.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_AuditFailure_When_HttpEnrollmentCompletes_Then_NoCookieOrRecoveryMaterialEscapes(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var cookies = new CookieContainer();
        using var browser = environment.Browser(cookies);
        var location = await AdministratorJourney.BeginAsync(environment, browser, enrollment: true);
        var form = await AdministratorJourney.ReadAsync(browser, location, enrollment: true);
        var code = FactorOracle.Code(form.Seed!, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await environment.Database.AuditPermissionAsync(false, TestContext.Current.CancellationToken);
        try
        {
            using var denied = await AdministratorJourney.ConfirmAsync(browser, form, code, enrollment: true);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, denied.StatusCode);
            Assert.Null(cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie]);
            var body = await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.False(body.Contains(RecoveryHeading, StringComparison.Ordinal) || body.Contains(form.Seed!, StringComparison.Ordinal));
        }
        finally { await environment.Database.AuditPermissionAsync(true, TestContext.Current.CancellationToken); }
        using var accepted = await AdministratorJourney.ConfirmAsync(browser, form, code, enrollment: true);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.NotNull(cookies.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie]);
    }

    /// <summary>Observed requests on both real nodes race one proof; only one commit survives and its discarded recovery body is never redisplayed.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_SameBoundEnrollment_When_TwoNodesContend_Then_OneCommitAndNoRecoveryRedisplayOccur(string provider)
    {
        await using var environment = await AdministratorEnvironment.CreateAsync(fixture, provider);
        var cookies = new CookieContainer();
        using var primary = environment.Browser(cookies);
        using var secondary = environment.Browser(cookies, secondary: true);
        var location = await AdministratorJourney.BeginAsync(environment, primary, enrollment: true);
        var form = await AdministratorJourney.ReadAsync(primary, location, enrollment: true);
        var code = FactorOracle.Code(form.Seed!, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var responses = await JourneyContention.CompleteAsync(environment, provider, primary, secondary, form, form, code, enrollment: true);
        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest));
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
        using var lostResponse = await secondary.GetAsync(location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, lostResponse.StatusCode);
        using var repeated = await AdministratorJourney.ConfirmAsync(secondary, form, code, enrollment: true);
        Assert.Equal(HttpStatusCode.BadRequest, repeated.StatusCode);
        await using var scope = environment.Database.Administrator.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await database.Set<AdministratorSession>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await database.Set<AdministratorReplay>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(10, await database.Set<AdministratorRecoveryCode>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await database.Set<SecurityAuditIntent>().CountAsync(row => row.Event == EnrollmentEvent && row.Outcome == Accepted, TestContext.Current.CancellationToken));
    }

    /// <summary>Uses actual native HTTP enrollment while retaining only in-memory synthetic material for a subsequent login control.</summary>
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
