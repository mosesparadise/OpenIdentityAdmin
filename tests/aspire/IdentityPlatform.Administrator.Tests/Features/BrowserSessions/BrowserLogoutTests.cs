using System.Net;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires genuine callback authentication, protected local logout, durable audit and bounded lost-response recovery on both stores.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLogoutTests(BootstrapFixture fixture)
{
    private const string CookieSeparator = "; ";
    private const string CookieDelimiter = ",";
    /// <summary>A callback-established B1 ticket is revoked with its browser generation and actor audit before any success cookies are returned.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_GenuineProductSession_When_ProtectedLogoutCommits_Then_OldCookiesDenyAndAuditRetainsActualActor(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        using var login = await scenario.Browser.GetAsync(await scenario.CallbackAsync(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, login.StatusCode);
        var original = scenario.Cookies.GetCookieHeader(scenario.Profile.ClientUri);
        using var status = await scenario.Browser.GetAsync(BrowserLogoutHttp.Status, TestContext.Current.CancellationToken);
        Assert.Contains("\"authenticated\":true", await status.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        var before = await scenario.SnapshotAsync();
        var token = await BrowserLogoutHttp.EntryAsync(scenario.Browser);
        Assert.Equal(before, await scenario.SnapshotAsync());
        using var response = await BrowserLogoutHttp.PostAsync(scenario.Browser, token);
        await BrowserLogoutHttp.CommittedAsync(response);
        await RequireCommittedAsync(scenario, authenticated: true);
        Assert.Null(scenario.Cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Session]);
        Assert.Null(scenario.Cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Prelogin]);
        Assert.Null(scenario.Cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Antiforgery]);
        await BrowserLogoutHttp.DeniedStatusAsync(scenario.Host, scenario.Profile.ClientUri, original);
        Assert.Equal(1, scenario.Host.Bridge.TokenSends);
        Assert.Equal(1, scenario.Host.Bridge.ParSends);
        _ = await scenario.StartAsync(await scenario.EntryAsync());
    }

    /// <summary>Discarded local logout cookies can be explicitly reconciled without a second transition, audit, budget update or lifetime extension.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_LostLogoutResponse_When_OriginalBrowserExplicitlyReconciles_Then_ImmutableTombstoneAndAllDurableStateAreUnchanged(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        _ = await scenario.StartAsync(await scenario.EntryAsync());
        var token = await BrowserLogoutHttp.EntryAsync(scenario.Browser);
        var saved = scenario.Cookies.GetCookieHeader(scenario.Profile.ClientUri);
        var detached = new CookieContainer();
        detached.SetCookies(scenario.Profile.ClientUri, saved.Replace(CookieSeparator, CookieDelimiter, StringComparison.Ordinal));
        using var delivery = scenario.Host.Browser(detached);
        using var lost = await BrowserLogoutHttp.PostAsync(delivery, token);
        await BrowserLogoutHttp.CommittedAsync(lost);
        await RequireCommittedAsync(scenario, authenticated: false);
        var before = await scenario.SnapshotAsync();
        using var wrongPurpose = await BrowserLogoutHttp.PostAsync(scenario.Browser, token);
        BrowserLoginTests.RequireDenied(wrongPurpose, HttpStatusCode.BadRequest);
        scenario.Issuer.Account.Journey.Clock.Now = scenario.Issuer.Account.Journey.Clock.GetUtcNow().AddSeconds(1);
        var recovery = await BrowserLogoutHttp.EntryAsync(scenario.Browser);
        using var reconciled = await BrowserLogoutHttp.PostAsync(scenario.Browser, recovery);
        await BrowserLogoutHttp.CommittedAsync(reconciled);
        Assert.Equal(before, await scenario.SnapshotAsync());
        using var retained = scenario.ReloadProtection();
        var rotated = new BrowserLoginProof(retained.Provider).Generation(
            scenario.Cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Generation]!.Value, scenario.Issuer.Account.Journey.Clock.GetUtcNow());
        Assert.NotNull(rotated);
        var tombstone = await scenario.ReadAsync(db => db.Set<BrowserContext>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(tombstone.ObservedAt, rotated.Created);
        Assert.Equal(tombstone.ExpiresAt, rotated.Expires);
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
        Assert.Equal(0, scenario.Host.Bridge.ParSends);
        _ = await scenario.StartAsync(await scenario.EntryAsync());
    }

    /// <summary>Reads actual persisted ticket provenance and the precise cancelled/established intent after local-only invalidation.</summary>
    internal static Task<bool> RequireCommittedAsync(BrowserLoginFixture scenario, bool authenticated) => scenario.ReadAsync(async database =>
    {
        var context = await database.Set<BrowserContext>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        const int LogoutGeneration = 2;
        Assert.Equal(LogoutGeneration, context.Generation);
        Assert.Null(context.TicketDigest);
        var audit = await database.Set<BrowserSessionAudit>().AsNoTracking().SingleAsync(row => row.Operation == BrowserMutation.Logout, TestContext.Current.CancellationToken);
        Assert.Equal(context.CorrelationId, audit.CorrelationId);
        Assert.Equal(context.Generation, audit.Generation);
        var ticket = await database.Set<AdminSessionTicket>().AsNoTracking().SingleOrDefaultAsync(TestContext.Current.CancellationToken);
        if (authenticated)
        {
            Assert.NotNull(ticket);
            Assert.NotNull(ticket.RevokedAt);
            Assert.Equal(ticket.Subject, audit.Subject);
            Assert.Equal(ticket.SourceSession, audit.SourceSession);
            var revoked = await database.Set<AdminSessionAudit>().AsNoTracking().SingleAsync(row => row.Operation == SessionMutation.Revoke, TestContext.Current.CancellationToken);
            Assert.Equal(ticket.Digest, revoked.Digest);
            Assert.Equal(ticket.Revision, revoked.Revision);
            Assert.Equal(SessionMutationOutcome.Committed, revoked.Outcome);
        }
        else
        {
            Assert.Null(ticket);
            Assert.Null(audit.Subject);
            Assert.Null(audit.SourceSession);
            Assert.Equal(BrowserIntentState.Cancelled, await database.Set<BrowserLoginIntent>().Select(row => row.State).SingleAsync(TestContext.Current.CancellationToken));
        }
        Assert.Equal(0, await database.Set<BrowserLogoutContinuation>().CountAsync(TestContext.Current.CancellationToken));
        return true;
    });
}
