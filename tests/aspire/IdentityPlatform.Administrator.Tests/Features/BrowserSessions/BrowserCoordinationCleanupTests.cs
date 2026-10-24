using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises child-first finite reclamation, actual dependency bounds and all-or-none cleanup with fresh provider readers.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserCoordinationCleanupTests(BootstrapFixture fixture)
{
    private const int CategoryCount = 3;
    private const int LateReservationMinutes = 4;
    private const int IntentMinutes = 5;
    private const int LatestNativeMinutes = 10;
    private const int ToleranceSeconds = 30;

    private const int ExtraRows = 7;
    private static readonly bool[] WinnerOrders = [true, false];
    private const string ProviderTrait = "Provider";

    /// <summary>Names both actual winner orders with stable native provider metadata.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Orders =>
        from provider in IssuerContinuationProviderFixture.Providers
        from cleanupFirst in WinnerOrders
        select new TheoryDataRow<string, bool>(provider, cleanupFirst).WithTrait(ProviderTrait, provider);

    /// <summary>A replacement that wins retains its new generation; cleanup that wins cannot let an old expected generation recreate a deleted context.</summary>
    [Theory, MemberData(nameof(Orders))]
    public async Task Given_ExpiredContextAndConcurrentReplacement_When_EachWriterOrderWins_Then_CleanupCannotResurrectOrDeleteNewAuthority(string provider, bool cleanupFirst)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        Assert.Equal(1, await scenario.StartAsync(browser, BrowserCoordinationFixture.Digest()));
        scenario.Clock.Set(scenario.Clock.GetUtcNow().AddMinutes(LatestNativeMinutes));
        var first = new NativeClientCallbackRace(typeof(BrowserSessionBudget));
        var second = new NativeClientCallbackRace(typeof(BrowserSessionBudget));
        Func<NativeClientCallbackRace, Task<bool>> cleanup = probe => scenario.RunAsync(async (database, _) =>
            await new BrowserSessionCleanup(database, scenario.Clock).RunAsync(BrowserCoordinationFixture.Token) > 0, probe);
        Func<NativeClientCallbackRace, Task<bool>> replace = async probe =>
            await scenario.StartAsync(browser, BrowserCoordinationFixture.Digest(), 1, faults: [probe]) is not null;
        var results = await BrowserCoordinationRaceTests.RaceAsync(scenario, first, second,
            () => cleanupFirst ? cleanup(first) : replace(first), () => cleanupFirst ? replace(second) : cleanup(second));
        Assert.True(results[0]);
        Assert.Equal(!cleanupFirst, results[1]);
        await scenario.RunAsync(async (database, _) =>
        {
            var context = await database.Set<BrowserContext>().SingleOrDefaultAsync(BrowserCoordinationFixture.Token);
            Assert.Equal(cleanupFirst, context is null);
            if (context is not null)
            {
                Assert.Equal(2, context.Generation);
            }
            Assert.Equal(cleanupFirst ? 0 : 1, await database.Set<BrowserLoginIntent>().CountAsync(BrowserCoordinationFixture.Token));
            return true;
        });
    }

    /// <summary>A real cleanup commit with lost acknowledgement retains its bounded deletions and audit history without recreating a spent generation.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CleanupCommitAcknowledgementLoss_When_StateIsReadAgain_Then_DeletionsRemainDurableAndAuditsRemainPresent(string provider)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        Assert.Equal(1, await scenario.StartAsync(browser, BrowserCoordinationFixture.Digest()));
        scenario.Clock.Set(scenario.Clock.GetUtcNow().AddMinutes(LatestNativeMinutes));
        var loss = new BrowserCommitLoss();
        Assert.Same(loss.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.RunAsync((database, _) =>
            new BrowserSessionCleanup(database, scenario.Clock).RunAsync(BrowserCoordinationFixture.Token), loss)));
        Assert.Equal(1, loss.Commits);
        Assert.Null(await scenario.StartAsync(browser, BrowserCoordinationFixture.Digest(), 1));
        await scenario.RunAsync(async (database, _) =>
        {
            Assert.Empty(await database.Set<BrowserContext>().ToArrayAsync(BrowserCoordinationFixture.Token));
            Assert.Empty(await database.Set<BrowserLoginIntent>().ToArrayAsync(BrowserCoordinationFixture.Token));
            Assert.Single(await database.Set<BrowserSessionAudit>().ToArrayAsync(BrowserCoordinationFixture.Token));
            return true;
        });
    }

    /// <summary>Each category removes only its hundred oldest expired rows; a failed final observation rolls back even already issued child deletes.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ExpiredChildren_When_CleanupRunsOrClockRollsBack_Then_BoundedChildFirstDeletionIsAtomic(string provider)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        await SeedAsync(scenario, BrowserCoordinationSeeds.Batch + ExtraRows);
        var before = await scenario.SnapshotAsync();
        var now = scenario.Clock.GetUtcNow();
        var rollback = new BrowserSaveObservation(() => scenario.Clock.Set(now.AddSeconds(-1)));
        Assert.Equal(0, await scenario.RunAsync((database, _) => new BrowserSessionCleanup(database, scenario.Clock).RunAsync(BrowserCoordinationFixture.Token), rollback));
        Assert.True(before == await scenario.SnapshotAsync());
        scenario.Clock.Set(now);
        Assert.Equal(BrowserCoordinationSeeds.Batch * CategoryCount, await CleanAsync(scenario));
        await scenario.RunAsync(async (database, _) =>
        {
            Assert.Equal(ExtraRows, await database.Set<BrowserLoginIntent>().CountAsync(BrowserCoordinationFixture.Token));
            Assert.Equal(ExtraRows, await database.Set<BrowserLogoutContinuation>().CountAsync(BrowserCoordinationFixture.Token));
            Assert.Equal(ExtraRows, await database.Set<BrowserContext>().CountAsync(BrowserCoordinationFixture.Token));
            Assert.Empty(await database.Set<BrowserSessionAudit>().ToArrayAsync(BrowserCoordinationFixture.Token));
            return true;
        });
        Assert.Equal(ExtraRows * CategoryCount, await CleanAsync(scenario));
        Assert.Equal(0, await CleanAsync(scenario));
    }

    /// <summary>Late challenge reservation retains markers through the latest permitted native creation plus its full lifetime and tolerance.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_LateReservation_When_IntentExpiresBeforeNativeDependency_Then_CleanupRetainsMarkersThroughTheLaterBound(string provider)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        var intent = BrowserCoordinationFixture.Digest();
        var reservation = BrowserCoordinationFixture.Digest();
        var start = scenario.Clock.GetUtcNow();
        Assert.Equal(1, await scenario.StartAsync(browser, intent));
        scenario.Clock.Set(start.AddMinutes(LateReservationMinutes));
        Assert.True(await scenario.ReserveAsync(browser, intent, 1, reservation));
        scenario.Clock.Set(start.AddMinutes(IntentMinutes));
        Assert.Equal(0, await CleanAsync(scenario));
        Assert.False(await scenario.EstablishAsync(browser, intent, 1, reservation, scenario.Ticket()));
        scenario.Clock.Set(start.AddMinutes(LatestNativeMinutes).AddSeconds(ToleranceSeconds));
        Assert.Equal(0, await CleanAsync(scenario));
        scenario.Clock.Set(scenario.Clock.GetUtcNow().AddTicks(1));
        Assert.Equal(2, await CleanAsync(scenario));
        Assert.False(await scenario.ReserveAsync(browser, intent, 1, reservation));
        Assert.Null(await scenario.StartAsync(browser, BrowserCoordinationFixture.Digest(), 1));
        await scenario.RunAsync(async (database, _) =>
        {
            Assert.Equal(2, await database.Set<BrowserSessionAudit>().CountAsync(BrowserCoordinationFixture.Token));
            return true;
        });
    }

    /// <summary>A committed ticket's actual absolute dependency retains its context even after the shorter original intent and idle deadlines.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_EstablishedTicket_When_OperationAndIdleExpire_Then_CleanupWaitsForOriginalAbsoluteDependency(string provider)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        var intent = BrowserCoordinationFixture.Digest();
        var reservation = BrowserCoordinationFixture.Digest();
        Assert.Equal(1, await scenario.StartAsync(browser, intent));
        Assert.True(await scenario.ReserveAsync(browser, intent, 1, reservation));
        var ticket = scenario.Ticket();
        Assert.True(await scenario.EstablishAsync(browser, intent, 1, reservation, ticket));
        scenario.Clock.Set(scenario.Clock.GetUtcNow().AddHours(1));
        Assert.Equal(0, await CleanAsync(scenario));
        scenario.Clock.Set(new DateTimeOffset(ticket.AbsoluteExpiresAt, TimeSpan.Zero).AddSeconds(ToleranceSeconds));
        Assert.Equal(0, await CleanAsync(scenario));
        scenario.Clock.Set(scenario.Clock.GetUtcNow().AddTicks(1));
        Assert.Equal(2, await CleanAsync(scenario));
        Assert.False(await scenario.CurrentAsync(browser, 1, ticket.Digest));
    }

    /// <summary>Creates independently expired rows with matching child timestamps so deterministic child-first batches can release their contexts.</summary>
    private static Task<int> SeedAsync(BrowserCoordinationFixture scenario, int count) => scenario.RunAsync(async (database, _) =>
    {
        for (var index = 0; index < count; index++)
        {
            var context = BrowserCoordinationSeeds.Context(scenario.Clock.GetUtcNow().UtcTicks + index, expired: true);
            database.Add(context);
            database.Add(BrowserCoordinationSeeds.Intent(context, live: false));
            database.Add(BrowserCoordinationSeeds.Logout(context, live: false));
        }
        return await database.SaveChangesAsync(BrowserCoordinationFixture.Token);
    });

    /// <summary>Runs the production cleanup in a fresh runtime context rather than trusting tracked deletion state.</summary>
    private static Task<int> CleanAsync(BrowserCoordinationFixture scenario) => scenario.RunAsync((database, _) =>
        new BrowserSessionCleanup(database, scenario.Clock).RunAsync(BrowserCoordinationFixture.Token));
}
