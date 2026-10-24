using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Checks independently counted retained/live ceilings under real multi-root quota ordering without evicting pending security state.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserCoordinationCapacityTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private const int NextGeneration = 2;

    /// <summary>Runs all three login admission ceilings and both remote-correlation ceilings with explicit native provider metadata.</summary>
    public static IEnumerable<TheoryDataRow<string, CoordinationCapacity>> Cases =>
        from provider in IssuerContinuationProviderFixture.Providers
        from capacity in Enum.GetValues<CoordinationCapacity>()
        select new TheoryDataRow<string, CoordinationCapacity>(provider, capacity).WithTrait(ProviderTrait, provider);

    /// <summary>At the last login slot only one peer commits; full remote capacity still permits durable local logout with an explicit incomplete remote result.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_FiniteCapacity_When_LastSlotIsContendedOrRemoteStoreIsFull_Then_NoLiveStateIsEvicted(string provider, CoordinationCapacity capacity)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        if (capacity is CoordinationCapacity.LiveLogout or CoordinationCapacity.RetainedLogout)
        {
            await RemoteCapacityAsync(scenario, capacity);
            return;
        }
        await SeedAsync(scenario, capacity, (capacity == CoordinationCapacity.LiveIntent ? BrowserCoordinationSeeds.Live : BrowserCoordinationSeeds.Retained) - 1);
        var retained = await SeedWitnessAsync(scenario);
        var first = new NativeClientCallbackRace(typeof(BrowserSessionBudget));
        var second = new NativeClientCallbackRace(typeof(BrowserSessionBudget));
        var results = await BrowserCoordinationRaceTests.RaceAsync(scenario, first, second,
            async () => await scenario.StartAsync(BrowserCoordinationFixture.Digest(), BrowserCoordinationFixture.Digest(), faults: [first]) == 1,
            async () => await scenario.StartAsync(BrowserCoordinationFixture.Digest(), BrowserCoordinationFixture.Digest(), faults: [second]) == 1);
        Assert.Equal([true, false], results);
        var after = await SeedWitnessAsync(scenario);
        Assert.All(retained, entry => Assert.True(after.TryGetValue(entry.Key, out var row) && row == entry.Value));
        var before = await scenario.SnapshotAsync();
        Assert.Null(await scenario.StartAsync(BrowserCoordinationFixture.Digest(), BrowserCoordinationFixture.Digest()));
        Assert.True(before == await scenario.SnapshotAsync());
    }

    /// <summary>Captures every existing seeded field so quota success cannot be credited after silently evicting or rewriting earlier rows.</summary>
    private static Task<Dictionary<string, string>> SeedWitnessAsync(BrowserCoordinationFixture scenario) => scenario.RunAsync(async (database, _) =>
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in await database.Set<BrowserContext>().AsNoTracking().ToArrayAsync(BrowserCoordinationFixture.Token))
        {
            result.Add(nameof(BrowserContext) + row.Digest, JsonSerializer.Serialize(row));
        }
        foreach (var row in await database.Set<BrowserLoginIntent>().AsNoTracking().ToArrayAsync(BrowserCoordinationFixture.Token))
        {
            result.Add(nameof(BrowserLoginIntent) + row.Digest, JsonSerializer.Serialize(row));
        }
        return result;
    });

    /// <summary>Fills only optional remote capacity, then independently proves committed ticket revocation and absence of an extra correlation.</summary>
    private static async Task RemoteCapacityAsync(BrowserCoordinationFixture scenario, CoordinationCapacity capacity)
    {
        var browser = BrowserCoordinationFixture.Digest();
        var intent = BrowserCoordinationFixture.Digest();
        var reservation = BrowserCoordinationFixture.Digest();
        Assert.Equal(1, await scenario.StartAsync(browser, intent));
        Assert.True(await scenario.ReserveAsync(browser, intent, 1, reservation));
        var ticket = scenario.Ticket();
        Assert.True(await scenario.EstablishAsync(browser, intent, 1, reservation, ticket));
        var limit = capacity == CoordinationCapacity.LiveLogout ? BrowserCoordinationSeeds.Live : BrowserCoordinationSeeds.Retained;
        await SeedAsync(scenario, capacity, limit);
        var retained = await LogoutWitnessAsync(scenario);
        var attempted = BrowserCoordinationFixture.Digest();
        var outcome = await scenario.RunAsync((database, codec) => new BrowserLogoutService(database, scenario.Clock, codec)
            .LogoutAsync(browser, 1, ticket.Digest, attempted, BrowserCoordinationFixture.Token));
        Assert.NotNull(outcome);
        Assert.Equal(NextGeneration, outcome.Generation);
        Assert.False(outcome.RemoteReserved);
        Assert.Equal(retained, await LogoutWitnessAsync(scenario));
        Assert.False(await scenario.CurrentAsync(browser, 1, ticket.Digest));
        await scenario.RunAsync(async (database, _) =>
        {
            Assert.Equal(limit, await database.Set<BrowserLogoutContinuation>().CountAsync(BrowserCoordinationFixture.Token));
            Assert.False(await database.Set<BrowserLogoutContinuation>().AnyAsync(row => row.Digest == attempted, BrowserCoordinationFixture.Token));
            Assert.NotNull((await database.Set<AdminSessionTicket>().SingleAsync(BrowserCoordinationFixture.Token)).RevokedAt);
            return true;
        });
    }

    /// <summary>Compares every retained remote field in stable order, detecting eviction, replacement and rewriting at either ceiling.</summary>
    private static Task<string> LogoutWitnessAsync(BrowserCoordinationFixture scenario) => scenario.RunAsync(async (database, _) =>
        JsonSerializer.Serialize(await database.Set<BrowserLogoutContinuation>().AsNoTracking().OrderBy(row => row.Digest)
            .ToArrayAsync(BrowserCoordinationFixture.Token)));

    /// <summary>Uses distinct contexts for live intents; historical and logout rows may share a context without granting any authentication.</summary>
    private static Task<int> SeedAsync(BrowserCoordinationFixture scenario, CoordinationCapacity capacity, int count) =>
        scenario.RunAsync(async (database, _) =>
        {
            var shared = BrowserCoordinationSeeds.Context(scenario.Clock.GetUtcNow().UtcTicks);
            if (capacity is not (CoordinationCapacity.Contexts or CoordinationCapacity.LiveIntent))
            {
                database.Add(shared);
            }
            for (var index = 0; index < count; index++)
            {
                if (capacity == CoordinationCapacity.Contexts)
                {
                    database.Add(BrowserCoordinationSeeds.Context(scenario.Clock.GetUtcNow().UtcTicks));
                }
                else if (capacity == CoordinationCapacity.LiveIntent)
                {
                    var context = BrowserCoordinationSeeds.Context(scenario.Clock.GetUtcNow().UtcTicks);
                    database.Add(context);
                    database.Add(BrowserCoordinationSeeds.Intent(context));
                }
                else if (capacity == CoordinationCapacity.RetainedIntent)
                {
                    database.Add(BrowserCoordinationSeeds.Intent(shared, live: false));
                }
                else
                {
                    database.Add(BrowserCoordinationSeeds.Logout(shared, live: capacity == CoordinationCapacity.LiveLogout));
                }
            }
            return await database.SaveChangesAsync(BrowserCoordinationFixture.Token);
        });
}

/// <summary>Names independent ceilings rather than importing the production quota values into test expectations.</summary>
public enum CoordinationCapacity
{
    LiveIntent, RetainedIntent, Contexts, LiveLogout, RetainedLogout
}
