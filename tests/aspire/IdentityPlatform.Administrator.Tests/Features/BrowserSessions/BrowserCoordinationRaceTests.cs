using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires actual database-session blocking before accepting either order of callback, logout and replacement.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserCoordinationRaceTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private const int NextGeneration = 2;

    private static readonly bool[] OrdersAllowed = [true, false];
    private static readonly bool[] OneWinner = [true, false];

    /// <summary>Retains both provider traits and both designated winner orders for each browser invalidation kind.</summary>
    public static IEnumerable<TheoryDataRow<string, bool, bool>> Orders =>
        from provider in IssuerContinuationProviderFixture.Providers
        from callbackFirst in OrdersAllowed
        from logout in OrdersAllowed
        select new TheoryDataRow<string, bool, bool>(provider, callbackFirst, logout).WithTrait(ProviderTrait, provider);

    /// <summary>Whichever operation takes the real writer lock first determines whether a ticket is created; invalidation always leaves it unusable.</summary>
    [Theory, MemberData(nameof(Orders))]
    public async Task Given_ConcurrentCallbackAndInvalidation_When_BothLockOrdersExecute_Then_TheWinningGenerationCannotBeResurrected(
        string provider, bool callbackFirst, bool logout)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        var intent = BrowserCoordinationFixture.Digest();
        var reservation = BrowserCoordinationFixture.Digest();
        var next = BrowserCoordinationFixture.Digest();
        Assert.Equal(1, await scenario.StartAsync(browser, intent));
        Assert.True(await scenario.ReserveAsync(browser, intent, 1, reservation));
        var ticket = scenario.Ticket();
        var first = new NativeClientCallbackRace(typeof(BrowserSessionBudget));
        var second = new NativeClientCallbackRace(typeof(BrowserSessionBudget));
        Func<NativeClientCallbackRace, Task<bool>> callback = probe => scenario.EstablishAsync(browser, intent, 1, reservation, ticket, probe);
        Func<NativeClientCallbackRace, Task<bool>> invalidate = async probe => logout
            ? await scenario.LogoutAsync(browser, 1, faults: [probe]) == NextGeneration
            : await scenario.StartAsync(browser, next, 1, faults: [probe]) == NextGeneration;
        var result = await RaceAsync(scenario, first, second,
            () => callbackFirst ? callback(first) : invalidate(first),
            () => callbackFirst ? invalidate(second) : callback(second));
        Assert.True(result[0]);
        Assert.Equal(callbackFirst, result[1]);
        Assert.False(await scenario.CurrentAsync(browser, 1, ticket.Digest));
        await scenario.RunAsync(async (database, _) =>
        {
            Assert.Equal(NextGeneration, (await database.Set<BrowserContext>().SingleAsync(BrowserCoordinationFixture.Token)).Generation);
            var stored = await database.Set<AdminSessionTicket>().SingleOrDefaultAsync(BrowserCoordinationFixture.Token);
            if (callbackFirst)
            {
                Assert.NotNull(stored?.RevokedAt);
            }
            else
            {
                Assert.Null(stored);
            }
            return true;
        });
    }

    /// <summary>Two independent roots cannot reserve or establish the same intent twice, even after both callers have its original identifiers.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_DuplicateConcurrentRequests_When_ReservationAndEstablishmentRace_Then_ExactlyOneOfEachCommits(string provider)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        var intent = BrowserCoordinationFixture.Digest();
        var reservation = BrowserCoordinationFixture.Digest();
        Assert.Equal(1, await scenario.StartAsync(browser, intent));
        var first = new NativeClientCallbackRace(typeof(BrowserSessionBudget));
        var second = new NativeClientCallbackRace(typeof(BrowserSessionBudget));
        Assert.Equal(OneWinner, await RaceAsync(scenario, first, second,
            () => scenario.ReserveAsync(browser, intent, 1, reservation, first),
            () => scenario.ReserveAsync(browser, intent, 1, BrowserCoordinationFixture.Digest(), second)));
        var winningTicket = scenario.Ticket();
        var losingTicket = scenario.Ticket();
        first = new(typeof(BrowserSessionBudget));
        second = new(typeof(BrowserSessionBudget));
        Assert.Equal(OneWinner, await RaceAsync(scenario, first, second,
            () => scenario.EstablishAsync(browser, intent, 1, reservation, winningTicket, first),
            () => scenario.EstablishAsync(browser, intent, 1, reservation, losingTicket, second)));
        await scenario.RunAsync(async (database, _) =>
        {
            var pending = await database.Set<BrowserLoginIntent>().SingleAsync(BrowserCoordinationFixture.Token);
            var ticket = await database.Set<AdminSessionTicket>().SingleAsync(BrowserCoordinationFixture.Token);
            Assert.True(pending.NativeStateDigest == reservation && pending.TicketDigest == winningTicket.Digest && ticket.Digest == winningTicket.Digest);
            Assert.Single(await database.Set<AdminSessionAudit>().ToArrayAsync(BrowserCoordinationFixture.Token));
            var audits = await database.Set<BrowserSessionAudit>().ToArrayAsync(BrowserCoordinationFixture.Token);
            Assert.Single(audits, audit => audit.Operation == BrowserMutation.Reserve);
            Assert.Single(audits, audit => audit.Operation == BrowserMutation.Establish && audit.Subject == winningTicket.Subject);
            return true;
        });
    }

    /// <summary>Reuses the existing failure-preserving join and exact native blocker observer for the new mapped Sessions budget.</summary>
    internal static Task<bool[]> RaceAsync(BrowserCoordinationFixture scenario, NativeClientCallbackRace winner,
        NativeClientCallbackRace peer, Func<Task<bool>> first, Func<Task<bool>> second)
    {
        var locked = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var peerLock = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        winner.Locked = locked;
        winner.Release = release.Task;
        peer.BeforeLock = peerLock;
        return NativeBlockedRace.InvokeAsync(first, locked.Task, second, peerLock.Task, release, scenario.Lease.ObserveAsync);
    }
}
