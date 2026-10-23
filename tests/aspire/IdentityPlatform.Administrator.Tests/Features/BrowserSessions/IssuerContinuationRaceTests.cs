using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Reuses native commit gating and provider wait-graph evidence; simultaneous task startup alone is not counted as contention.</summary>
[Collection("administrator-bootstrap")]
public sealed class IssuerContinuationRaceTests(BootstrapFixture fixture)
{
    private const string AdmissionWait = "%UPDATE%AdministratorAdmission%";
    private const int PeerCount = 1;
    private const int LastLiveSlot = 127;

    /// <summary>Two independent roots serialize conflicting anonymous admission, yielding one real commit and one unchanged denial.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_CompetingContinuationStarts_When_ActualAdmissionLockBlocksThePeer_Then_OnlyOneConflictingEntryAndAuditCommit(string provider, EntryRace mode)
    {
        var gate = new NativeCommitGate();
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(gate)));
        await using var peerRoot = scenario.CreatePeer(provider, _ => { });
        if (mode == EntryRace.LastLiveSlot)
        {
            await IssuerContinuationCapacityTests.FillAsync(scenario, LastLiveSlot, expired: false);
        }
        var first = IssuerContinuationProviderFixture.Create();
        var second = Peer(first, mode);
        await using var firstScope = scenario.CreateScope();
        await using var secondScope = peerRoot.Services.CreateAsyncScope();
        gate.Arm(firstScope.ServiceProvider.GetRequiredService<PlatformDbContext>());
        var results = await ContendAsync(scenario, gate,
            firstScope.ServiceProvider.GetRequiredService<IssuerContinuationService>(), first,
            secondScope.ServiceProvider.GetRequiredService<IssuerContinuationService>(), second);
        Assert.True(results[0]);
        Assert.False(results[1]);
        await using var read = scenario.CreateScope();
        var database = read.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(1, await database.Set<SecurityAuditIntent>().CountAsync(row => row.Event == IssuerContinuationProviderFixture.StartEvent, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(mode == EntryRace.LastLiveSlot ? LastLiveSlot + 1 : 1, await database.Set<IssuerContinuation>().CountAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(await database.Set<IssuerContinuation>().AnyAsync(row => row.IntentDigest == first.Request.IntentDigest, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Chooses only the tested collision while retaining independent values for all other request proofs.</summary>
    private static IssuerContinuationProviderFixture.Attempt Peer(IssuerContinuationProviderFixture.Attempt first, EntryRace mode)
    {
        var different = IssuerContinuationProviderFixture.Create();
        return mode switch
        {
            EntryRace.Intent => new(first.Intent, different.Browser, different.Budget, different.Correlation),
            EntryRace.Browser => new(different.Intent, first.Browser, different.Budget, different.Correlation),
            _ => different
        };
    }

    /// <summary>Holds the actual first commit, proves the second runtime UPDATE waits on it, then releases and joins both owners on every path.</summary>
    private static async Task<bool[]> ContendAsync(JourneyScenario scenario, NativeCommitGate gate,
        IssuerContinuationService first, IssuerContinuationProviderFixture.Attempt firstRequest,
        IssuerContinuationService second, IssuerContinuationProviderFixture.Attempt secondRequest)
    {
        var token = TestContext.Current.CancellationToken;
        var winner = Task.Run(() => first.StartAsync(firstRequest.Request, token), token);
        Task<bool>? peer = null;
        ExceptionDispatchInfo? failure = null;
        try
        {
            var blocker = await gate.WaitAsync(winner, token);
            peer = Task.Run(() => second.StartAsync(secondRequest.Request, token), token);
            await DatabaseWaiters.ObserveOwnedAsync(scenario.Lease, blocker, AdmissionWait, PeerCount, token);
            Assert.False(winner.IsCompleted);
            Assert.False(peer.IsCompleted);
        }
        catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
        finally { gate.Release(); }
        var joined = await Record.ExceptionAsync(async () =>
        {
            if (peer is null)
            {
                await winner;
            }
            else
            {
                await Task.WhenAll(winner, peer);
            }
        });
        if (failure is not null && joined is not null)
        {
            throw new AggregateException(failure.SourceException, joined);
        }
        failure?.Throw();
        if (joined is not null)
        {
            ExceptionDispatchInfo.Capture(joined).Throw();
        }
        return [await winner, await peer!];
    }

    /// <summary>Discovers each contention invariant independently on both real providers.</summary>
    public static IEnumerable<TheoryDataRow<string, EntryRace>> Cases =>
        from provider in IssuerContinuationProviderFixture.Providers
        from mode in Enum.GetValues<EntryRace>()
        select new TheoryDataRow<string, EntryRace>(provider, mode).WithTrait(IssuerContinuationProviderFixture.ProviderTrait, provider);
}

/// <summary>Names independent uniqueness and live-capacity contention boundaries.</summary>
public enum EntryRace
{
    Intent, Browser, LastLiveSlot
}
