using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Proves durable browser ordering and real protected actor provenance through independently reopened provider contexts.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserCoordinationTests(BootstrapFixture fixture)
{
    private const int NextGeneration = 2;
    private const int TransitionCount = 3;
    private const int IntentMinutes = 5;
    private const int ToleranceSeconds = 30;

    /// <summary>A single reserved intent creates one protected ticket and actor audit; repetition changes neither state nor audit.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ReservedIntent_When_EstablishedOnce_Then_TicketGenerationAndActorAuditCommitTogether(string provider)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        var intent = BrowserCoordinationFixture.Digest();
        var reservation = BrowserCoordinationFixture.Digest();
        Assert.Equal(1, await scenario.StartAsync(browser, intent));
        Assert.True(await scenario.ReserveAsync(browser, intent, 1, reservation));
        var ticket = scenario.Ticket();
        Assert.True(await scenario.EstablishAsync(browser, intent, 1, reservation, ticket));
        Assert.True(await scenario.CurrentAsync(browser, 1, ticket.Digest));
        Assert.False(await scenario.CurrentAsync(browser, NextGeneration, ticket.Digest));
        await RequireEstablishedAsync(scenario, browser, intent, ticket);
        var before = await scenario.SnapshotAsync();
        Assert.False(await scenario.ReserveAsync(browser, intent, 1, reservation));
        Assert.False(await scenario.EstablishAsync(browser, intent, 1, reservation, scenario.Ticket()));
        Assert.True(before == await scenario.SnapshotAsync());
    }

    /// <summary>Replacement revokes the old ticket and generation without treating an anonymous proof as the old subject's authentication.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CommittedTicket_When_AnonymousReplacementStarts_Then_OldAuthorityIsRevokedWithoutActorImpersonation(string provider)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        var intent = BrowserCoordinationFixture.Digest();
        var reservation = BrowserCoordinationFixture.Digest();
        Assert.Equal(1, await scenario.StartAsync(browser, intent));
        Assert.True(await scenario.ReserveAsync(browser, intent, 1, reservation));
        var ticket = scenario.Ticket();
        Assert.True(await scenario.EstablishAsync(browser, intent, 1, reservation, ticket));
        Assert.Equal(NextGeneration, await scenario.StartAsync(browser, BrowserCoordinationFixture.Digest(), 1));
        Assert.False(await scenario.CurrentAsync(browser, 1, ticket.Digest));
        await scenario.RunAsync(async (database, _) =>
        {
            Assert.NotNull((await database.Set<AdminSessionTicket>().SingleAsync(BrowserCoordinationFixture.Token)).RevokedAt);
            var audit = await database.Set<BrowserSessionAudit>().SingleAsync(row => row.Generation == NextGeneration, BrowserCoordinationFixture.Token);
            Assert.Null(audit.Subject);
            Assert.Null(audit.SourceSession);
            Assert.Equal(BrowserMutation.Start, audit.Operation);
            return true;
        });
        var before = await scenario.SnapshotAsync();
        Assert.Null(await scenario.StartAsync(browser, BrowserCoordinationFixture.Digest(), 1));
        Assert.False(await scenario.EstablishAsync(browser, intent, 1, reservation, scenario.Ticket()));
        Assert.True(before == await scenario.SnapshotAsync());
    }

    /// <summary>Local logout is durable before remote navigation and a one-use return remains explicitly unverified.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_AuthenticatedLocalLogout_When_RemoteReturnArrives_Then_LocalRevocationPersistsAndRemoteOutcomeStaysUnverified(string provider)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        var intent = BrowserCoordinationFixture.Digest();
        var reservation = BrowserCoordinationFixture.Digest();
        var correlation = BrowserCoordinationFixture.Digest();
        Assert.Equal(1, await scenario.StartAsync(browser, intent));
        Assert.True(await scenario.ReserveAsync(browser, intent, 1, reservation));
        var ticket = scenario.Ticket();
        Assert.True(await scenario.EstablishAsync(browser, intent, 1, reservation, ticket));
        Assert.Equal(NextGeneration, await scenario.LogoutAsync(browser, 1, ticket.Digest, correlation));
        Assert.False(await scenario.CurrentAsync(browser, 1, ticket.Digest));
        Assert.False(await RemoteAsync(scenario, browser, correlation, returned: true));
        Assert.True(await RemoteAsync(scenario, browser, correlation, returned: false));
        Assert.True(await RemoteAsync(scenario, browser, correlation, returned: true));
        Assert.False(await RemoteAsync(scenario, browser, correlation, returned: true));
        await scenario.RunAsync(async (database, _) =>
        {
            var row = await database.Set<BrowserLogoutContinuation>().SingleAsync(BrowserCoordinationFixture.Token);
            Assert.Equal(BrowserLogoutState.ReturnedUnverified, row.State);
            Assert.Equal(TimeSpan.FromMinutes(IntentMinutes).Ticks, row.ExpiresAt - row.CreatedAt);
            var audit = await database.Set<BrowserSessionAudit>().SingleAsync(value => value.Operation == BrowserMutation.Logout, BrowserCoordinationFixture.Token);
            Assert.True(audit.Subject == ticket.Subject && audit.SourceSession == ticket.SourceSession);
            Assert.Null((await database.Set<BrowserContext>().SingleAsync(BrowserCoordinationFixture.Token)).TicketDigest);
            return true;
        });
    }

    /// <summary>Calls the actual remote correlation transition with the original logged-out generation.</summary>
    private static Task<bool> RemoteAsync(BrowserCoordinationFixture scenario, string browser, string correlation, bool returned) =>
        scenario.RunAsync((database, codec) => new BrowserLogoutService(database, scenario.Clock, codec)
            .RemoteAsync(browser, correlation, NextGeneration, returned, BrowserCoordinationFixture.Token));

    /// <summary>Checks exact state relationships, original deadline, retained dependency and real envelope authentication after disposal.</summary>
    private static Task<bool> RequireEstablishedAsync(BrowserCoordinationFixture scenario, string browser, string intent, AdminSessionTicket ticket) =>
        scenario.RunAsync(async (database, codec) =>
        {
            var context = await database.Set<BrowserContext>().SingleAsync(BrowserCoordinationFixture.Token);
            var pending = await database.Set<BrowserLoginIntent>().SingleAsync(BrowserCoordinationFixture.Token);
            var stored = await database.Set<AdminSessionTicket>().SingleAsync(BrowserCoordinationFixture.Token);
            Assert.True(context.Digest == browser && context.TicketDigest == stored.Digest && pending.Digest == intent && pending.TicketDigest == stored.Digest);
            Assert.Equal(BrowserIntentState.Established, pending.State);
            Assert.Equal(TimeSpan.FromMinutes(IntentMinutes).Ticks, pending.ExpiresAt - pending.CreatedAt);
            Assert.True(pending.RetainUntil >= ticket.AbsoluteExpiresAt + TimeSpan.FromSeconds(ToleranceSeconds).Ticks);
            Assert.True(codec.IsAuthentic(stored, scenario.Clock.GetUtcNow()));
            Assert.Single(await database.Set<AdminSessionAudit>().ToArrayAsync(BrowserCoordinationFixture.Token));
            var audits = await database.Set<BrowserSessionAudit>().ToArrayAsync(BrowserCoordinationFixture.Token);
            Assert.Equal(TransitionCount, audits.Length);
            Assert.All(audits.Where(row => row.Operation != BrowserMutation.Establish), row => Assert.Null(row.Subject));
            var actor = Assert.Single(audits, row => row.Operation == BrowserMutation.Establish);
            Assert.True(actor.Subject == stored.Subject && actor.SourceSession == stored.SourceSession);
            return true;
        });
}
