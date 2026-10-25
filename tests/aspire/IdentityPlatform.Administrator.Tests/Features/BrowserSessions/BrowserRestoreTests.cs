using System.Net;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Demonstrates real B3 resurrection after selective row restoration, then requires explicit invalidation at every affected boundary.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserRestoreTests(BootstrapFixture fixture)
{
    private const string CookieSeparator = "; ";
    private const string CookieDelimiter = ",";
    private const string AuthenticatedStatus = "\"authenticated\":true";

    /// <summary>Preserved authentic cookies regain authority after exact restored rows; explicit local/source/profile/native invalidation independently denies reuse.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_LoggedOutGenuineBrowser_When_ExactRowsAreRestoredThenInvalidated_Then_ResurrectionIsDemonstratedAndEveryBoundaryRemainsRevoked(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var callback = await scenario.CallbackAsync();
        var nativeCookies = scenario.Cookies.GetCookieHeader(callback);
        var state = await NativeTokenSnapshot.CaptureAsync(scenario.Host.Services, clientState: true);
        using var login = await scenario.Browser.GetAsync(callback, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, login.StatusCode);
        var original = scenario.Cookies.GetCookieHeader(scenario.Profile.ClientUri);
        var snapshot = await BrowserRestoreSnapshot.CaptureAsync(scenario);
        await RequireAuthenticatedAsync(scenario.Browser);
        using var firstLogout = await BrowserLogoutHttp.PostAsync(scenario.Browser, await BrowserLogoutHttp.EntryAsync(scenario.Browser));
        await BrowserLogoutHttp.CommittedAsync(firstLogout);
        await BrowserLogoutHttp.DeniedStatusAsync(scenario.Host, scenario.Profile.ClientUri, original);
        await snapshot.RestoreAsync(scenario);
        await snapshot.RequireOriginalAsync(scenario);
        var restoredCookies = new CookieContainer();
        restoredCookies.SetCookies(scenario.Profile.ClientUri, original.Replace(CookieSeparator, CookieDelimiter, StringComparison.Ordinal));
        using var restored = scenario.Host.Browser(restoredCookies);
        await RequireAuthenticatedAsync(restored);
        var actor = await scenario.ReadAsync(db => db.Set<AdminSessionTicket>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
        using var finalLogout = await BrowserLogoutHttp.PostAsync(restored, await BrowserLogoutHttp.EntryAsync(restored));
        await BrowserLogoutHttp.CommittedAsync(finalLogout);
        await RevokeSourceAsync(scenario, actor);
        await NativeRestoreInvalidation.ApplyAsync(scenario.Issuer);
        await state.RevokeAsync(scenario.Host.Services);
        await NativeRestoreInvalidation.RequireAsync(scenario.Issuer);
        await state.RequireStatusAsync(scenario.Host.Services, OpenIddictConstants.Statuses.Revoked);
        await RequireLocalAndSourceAsync(scenario, actor);
        await BrowserLogoutHttp.DeniedStatusAsync(scenario.Host, scenario.Profile.ClientUri, original);
        var before = await scenario.SnapshotAsync();
        using var replay = await BrowserCallbackTests.ReplayAsync(scenario.Host, callback, nativeCookies);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(1, scenario.Host.Bridge.TokenSends);
        Assert.Equal(1, scenario.Host.Bridge.ParSends);
    }

    /// <summary>Requires actual authenticated JSON from the product endpoint before logout and before restored-boundary mitigation.</summary>
    private static async Task RequireAuthenticatedAsync(HttpClient browser)
    {
        using var response = await browser.GetAsync(BrowserLogoutHttp.Status, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(AuthenticatedStatus, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    /// <summary>Revokes the fresh B3 source session through ordinary authority/audit checks, independently of the fixture's older setup session.</summary>
    private static async Task RevokeSourceAsync(BrowserLoginFixture scenario, AdminSessionTicket ticket)
    {
        Assert.NotEqual(scenario.Issuer.Account.Session.SessionId, ticket.SourceSession);
        await using var scope = scenario.Issuer.Account.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<JourneyLogout>().RevokeAsync(
            new JourneySessionCandidate(ticket.Subject, ticket.SourceSession), TestContext.Current.CancellationToken));
    }

    /// <summary>Inspects the actual fresh issuer row and local tombstone/ticket/audit history rather than inferring all effects from one early denial.</summary>
    private static async Task RequireLocalAndSourceAsync(BrowserLoginFixture scenario, AdminSessionTicket original)
    {
        await using var source = scenario.Issuer.Account.CreateScope();
        var issuer = source.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await issuer.Set<AdministratorSession>().Where(row => row.PublicId == original.SourceSession && row.UserId == original.Subject)
            .Select(row => row.Revoked).SingleAsync(TestContext.Current.CancellationToken));
        await scenario.ReadAsync(async database =>
        {
            const int LogoutGeneration = 2;
            const int RetainedLogoutAudits = 2;
            var context = await database.Set<BrowserContext>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(LogoutGeneration, context.Generation);
            Assert.Null(context.TicketDigest);
            Assert.NotNull(await database.Set<AdminSessionTicket>().Select(row => row.RevokedAt).SingleAsync(TestContext.Current.CancellationToken));
            Assert.False(await database.Set<BrowserLoginIntent>().AnyAsync(row => row.State == BrowserIntentState.Pending || row.State == BrowserIntentState.Reserved,
                TestContext.Current.CancellationToken));
            Assert.Equal(RetainedLogoutAudits, await database.Set<BrowserSessionAudit>().CountAsync(row => row.Operation == BrowserMutation.Logout, TestContext.Current.CancellationToken));
            Assert.Equal(RetainedLogoutAudits, await database.Set<AdminSessionAudit>().CountAsync(row => row.Operation == SessionMutation.Revoke, TestContext.Current.CancellationToken));
            return true;
        });
    }
}
