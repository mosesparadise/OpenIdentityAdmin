using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises independent live/retained boundaries and strictly bounded physical reclamation using actual provider rows.</summary>
[Collection("administrator-bootstrap")]
public sealed class IssuerContinuationCapacityTests(BootstrapFixture fixture)
{
    private static readonly bool[] RetentionModes = [false, true];
    private const int LiveLimit = 128;
    private const int RetainedLimit = 10000;
    private const int CleanupLimit = 100;
    private const int LifetimeSeconds = 300;
    private const int ToleranceSeconds = 30;
    private const int BatchSize = 250;
    private const string Client = "identity-admin-bff";
    private const string Attached = "attached";

    /// <summary>Exactly one final slot remains admissible; a further distinct intent/browser denies without deleting retained state.</summary>
    [Theory, MemberData(nameof(CapacityCases))]
    public async Task Given_OneRemainingSlot_When_AnonymousAdmissionRunsTwice_Then_OnlyTheInclusiveCapacityBoundaryCommits(string provider, bool retained)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var limit = retained ? RetainedLimit : LiveLimit;
        await FillAsync(scenario, limit - 1, retained);
        var final = IssuerContinuationProviderFixture.Create();
        Assert.True(await IssuerContinuationProviderFixture.StartAsync(scenario, final));
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        Assert.False(await IssuerContinuationProviderFixture.StartAsync(scenario, IssuerContinuationProviderFixture.Create()));
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
        await using var scope = scenario.CreateScope();
        Assert.Equal(limit, await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<IssuerContinuation>().CountAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Neither an existing intent nor a second intent using the same live issuer-browser proof creates another row or audit.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_LiveBrowserContinuation_When_IntentOrBrowserIsReused_Then_AdmissionDeniesWithoutReplacement(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        var first = IssuerContinuationProviderFixture.Create();
        Assert.True(await IssuerContinuationProviderFixture.StartAsync(scenario, first));
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        Assert.False(await IssuerContinuationProviderFixture.StartAsync(scenario, first));
        var second = new IssuerContinuationProviderFixture.Attempt(IssuerContinuationProviderFixture.Selector(), first.Browser, first.Budget, first.Correlation);
        Assert.False(await IssuerContinuationProviderFixture.StartAsync(scenario, second));
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
    }

    /// <summary>Cleanup excludes exact retention equality, future watermark and live rows, and removes no more than one hundred eligible tombstones.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_MixedRetainedState_When_CleanupRuns_Then_OnlyOneStrictlyEligibleBoundedBatchIsRemoved(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        await FillAsync(scenario, CleanupLimit + 1, expired: true);
        var now = scenario.Clock.Now.ToUnixTimeSeconds();
        var equality = Row(now, expired: true);
        equality.RetainUntil = now;
        var future = Row(now, expired: true);
        future.ObservedAt = now + 1;
        var live = Row(now, expired: false);
        await using (var seed = scenario.CreateScope())
        {
            var database = seed.ServiceProvider.GetRequiredService<PlatformDbContext>();
            database.AddRange(equality, future, live);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var scope = scenario.CreateScope();
        var cleaner = scope.ServiceProvider.GetRequiredService<IssuerContinuationService>();
        Assert.Equal(CleanupLimit, await cleaner.CleanupAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await cleaner.CleanupAsync(TestContext.Current.CancellationToken));
        var remaining = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<IssuerContinuation>().AsNoTracking().ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(remaining.Select(row => row.IntentDigest).ToHashSet(StringComparer.Ordinal)
            .SetEquals([equality.IntentDigest, future.IntentDigest, live.IntentDigest]));
    }

    /// <summary>Seeds disposable occupancy in small committed fixture batches; no synthetic row serves as authentication proof.</summary>
    internal static async Task FillAsync(JourneyScenario scenario, int count, bool expired)
    {
        for (var offset = 0; offset < count; offset += BatchSize)
        {
            await using var scope = scenario.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            database.AddRange(Enumerable.Range(0, Math.Min(BatchSize, count - offset))
                .Select(_ => Row(scenario.Clock.Now.ToUnixTimeSeconds(), expired)));
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Creates only capacity metadata with independently specified limits and original deadline ordering.</summary>
    private static IssuerContinuation Row(long now, bool expired)
    {
        var created = expired ? now - LifetimeSeconds - ToleranceSeconds - 1 : now;
        return new IssuerContinuation
        {
            IntentDigest = IssuerContinuationProviderFixture.Digest(IssuerContinuationProviderFixture.Selector()),
            BrowserDigest = IssuerContinuationProviderFixture.Digest(IssuerContinuationProviderFixture.Selector()),
            ClientId = Client,
            State = expired ? Attached : IssuerContinuationProviderFixture.Pending,
            CreatedAt = created,
            ObservedAt = created,
            ExpiresAt = created + LifetimeSeconds,
            RetainUntil = created + LifetimeSeconds + ToleranceSeconds,
            Revision = 1,
            CorrelationId = Guid.NewGuid()
        };
    }

    /// <summary>Discovers both independent capacities on each installed provider.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> CapacityCases =>
        from provider in IssuerContinuationProviderFixture.Providers
        from retained in RetentionModes
        select new TheoryDataRow<string, bool>(provider, retained).WithTrait(IssuerContinuationProviderFixture.ProviderTrait, provider);
}
