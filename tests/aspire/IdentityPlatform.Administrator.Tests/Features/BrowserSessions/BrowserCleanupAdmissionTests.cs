using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Races a genuine protected BFF start against eligible cleanup at retained capacity, requiring both actual provider lock orders.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserCleanupAdmissionTests(BootstrapFixture fixture)
{
    private const string ProviderTrait = "Provider";
    private const string SetCookie = "Set-Cookie";
    private static readonly bool[] Orders = [false, true];

    /// <summary>Discovers each database and winner order without treating synthetic capacity rows as authenticated browser evidence.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Cases => from provider in IssuerContinuationProviderFixture.Providers
                                                                    from cleanupFirst in Orders
                                                                    select new TheoryDataRow<string, bool>(provider, cleanupFirst).WithTrait(ProviderTrait, provider);

    /// <summary>A cleanup winner admits one real start; an admission loser fails without eviction and can succeed only after a separate explicit retry.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_FullRetainedCapacity_When_ProtectedStartAndCleanupContend_Then_OnlyFreedCapacityAdmitsWithoutChangingRetainedRows(string provider, bool cleanupFirst)
    {
        var startProbe = new NativeClientCallbackRace(typeof(BrowserSessionBudget));
        var cleanupProbe = new NativeClientCallbackRace(typeof(BrowserSessionBudget));
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider, startProbe.Configure);
        var antiforgery = await scenario.EntryAsync();
        var retained = await SeedAsync(scenario);
        await using var cleanup = await scenario.PeerAsync(cleanupProbe.Configure);
        var locked = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var winner = cleanupFirst ? cleanupProbe : startProbe;
        var loser = cleanupFirst ? startProbe : cleanupProbe;
        winner.Locked = locked;
        winner.Release = release.Task;
        loser.BeforeLock = waiting;
        Func<Task<bool>> start = () => StartAsync(scenario, antiforgery, cleanupFirst);
        Func<Task<bool>> reclaim = () => CleanupAsync(scenario, cleanup);
        var results = await NativeBlockedRace.InvokeAsync(cleanupFirst ? reclaim : start, locked.Task,
            cleanupFirst ? start : reclaim, waiting.Task, release, scenario.Database.ObserveAsync);
        Assert.Equal(cleanupFirst, results[0]);
        Assert.True(results[1]);
        if (!cleanupFirst)
        {
            await RequireDeniedAsync(scenario);
            _ = await scenario.StartAsync(antiforgery);
        }
        await RequireAdmittedAsync(scenario, retained);
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
        Assert.Equal(0, scenario.Host.Bridge.ParSends);
        Assert.Equal(0, cleanup.Bridge.TokenSends);
        Assert.Equal(0, cleanup.Bridge.ParSends);
    }

    /// <summary>Executes the actual native-CSRF protected HTTP action and asserts the status for the independently chosen writer order.</summary>
    private static async Task<bool> StartAsync(BrowserLoginFixture scenario, string antiforgery, bool admitted)
    {
        using var response = await scenario.PostAsync(antiforgery);
        Assert.Equal(admitted ? HttpStatusCode.SeeOther : HttpStatusCode.BadRequest, response.StatusCode);
        if (!admitted)
        {
            Assert.Null(response.Headers.Location);
            Assert.False(response.Headers.Contains(SetCookie));
        }
        return response.StatusCode == HttpStatusCode.SeeOther;
    }

    /// <summary>Runs the production cleanup in its independent observer root and proves exactly one expired context was reclaimed.</summary>
    private static async Task<bool> CleanupAsync(BrowserLoginFixture scenario, BrowserLoginHost cleanup)
    {
        await using var scope = cleanup.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        Assert.Equal(1, await new BrowserSessionCleanup(database, scenario.Issuer.Account.Journey.Clock).RunAsync(TestContext.Current.CancellationToken));
        return true;
    }

    /// <summary>Seeds inert quota occupancy and one genuinely eligible expired context, retaining only private digests for later full-field comparison.</summary>
    private static Task<Dictionary<string, string>> SeedAsync(BrowserLoginFixture scenario) => scenario.ReadAsync(async database =>
    {
        var retained = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < BrowserCoordinationSeeds.Retained - 1; index++)
        {
            var context = BrowserCoordinationSeeds.Context(scenario.Issuer.Account.Journey.Clock.GetUtcNow().UtcTicks);
            database.Add(context);
            retained.Add(context.Digest, Hash(context));
        }
        database.Add(BrowserCoordinationSeeds.Context(scenario.Issuer.Account.Journey.Clock.GetUtcNow().UtcTicks, expired: true));
        Assert.Equal(BrowserCoordinationSeeds.Retained, await database.SaveChangesAsync(TestContext.Current.CancellationToken));
        return retained;
    });

    /// <summary>Reads every retained context and the sole genuine pending intent/start audit after the successful product admission.</summary>
    private static Task<bool> RequireAdmittedAsync(BrowserLoginFixture scenario, Dictionary<string, string> retained) => scenario.ReadAsync(async database =>
    {
        var contexts = await database.Set<BrowserContext>().AsNoTracking().ToDictionaryAsync(row => row.Digest, TestContext.Current.CancellationToken);
        Assert.Equal(BrowserCoordinationSeeds.Retained, contexts.Count);
        Assert.All(retained, pair => Assert.True(contexts.TryGetValue(pair.Key, out var row) && pair.Value == Hash(row)));
        var intent = await database.Set<BrowserLoginIntent>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(BrowserIntentState.Pending, intent.State);
        Assert.False(retained.ContainsKey(intent.BrowserDigest));
        Assert.Equal(1, intent.Generation);
        Assert.Null(contexts[intent.BrowserDigest].TicketDigest);
        var audit = await database.Set<BrowserSessionAudit>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(BrowserMutation.Start, audit.Operation);
        Assert.Equal(intent.CorrelationId, audit.CorrelationId);
        Assert.Null(audit.Subject);
        Assert.Empty(await database.Set<AdminSessionTicket>().ToArrayAsync(TestContext.Current.CancellationToken));
        return true;
    });

    /// <summary>A denied start must leave no intent, ticket or mutation audit even though the later cleanup has already freed capacity.</summary>
    private static Task<bool> RequireDeniedAsync(BrowserLoginFixture scenario) => scenario.ReadAsync(async database =>
    {
        Assert.Equal(BrowserCoordinationSeeds.Retained - 1, await database.Set<BrowserContext>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await database.Set<BrowserLoginIntent>().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await database.Set<BrowserSessionAudit>().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await database.Set<AdminSessionTicket>().ToArrayAsync(TestContext.Current.CancellationToken));
        return true;
    });

    /// <summary>Compares all mapped row fields without placing private browser digests in assertion output.</summary>
    private static string Hash(BrowserContext context) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(context)));
}
