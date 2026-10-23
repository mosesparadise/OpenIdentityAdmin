using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires real profile-lock contention for two fresh native demands competing for the same genuine completion.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeBrowserParRaceTests(BootstrapFixture fixture)
{
    private const string ProfileWait = "%UPDATE%AdministratorProtocolProfile%";
    private const int OnePeer = 1;
    private const int CompetingAssertions = 2;

    /// <summary>Only the held winner attaches its actual token ID; the independently authenticated peer cannot mint another request.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_OneCompletionAcrossTwoIssuers_When_ActualNativeParCommitsContend_Then_OnlyTheWinnerTokenAttaches(string provider)
    {
        var gate = new NativeCommitGate();
        var observed = new NativeBrowserParObservation { AfterPersisted = gate.Arm };
        await using var flow = await NativeBrowserParFixture.CreateAsync(fixture, provider, configure: services =>
        {
            observed.Configure(services);
            services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(gate));
        });
        await using var peer = flow.Scenario.CreatePeer(provider, services => services.AddBrowserCompletionPar(), flow.Scenario.Material.Policy.ForBrowserSessions());
        _ = await NativeAccountFlow.PushAsync(flow.Scenario, flow.Services);
        _ = await NativeAccountFlow.PushAsync(flow.Scenario, peer.Services);
        var before = await flow.RowAsync();
        var counts = await NativeAccountPersistence.CountAsync(flow.Scenario);
        var assertions = await flow.AssertionsAsync();
        observed.Armed = true;
        NativeAccountResult[] results;
        try
        {
            results = await ContendAsync(flow, peer.Services, gate);
        }
        finally { observed.Armed = false; }
        Assert.Equal(1, observed.Reached);
        _ = await flow.RequireAttachedAsync(results[0], before, flow.Journey.Clock.Now.ToUnixTimeSeconds());
        NativeBrowserParFixture.RequireUnavailable(results[1]);
        Assert.True((await flow.RowAsync()).RequestId == observed.Identifier);
        Assert.Equal(assertions + CompetingAssertions, await flow.AssertionsAsync());
        Assert.Equal((counts.Tokens + 1, counts.Authorizations), await NativeAccountPersistence.CountAsync(flow.Scenario));
    }

    /// <summary>Schedules both complete native invocations, proves a runtime profile UPDATE waits on the exact winner, then joins every owner before disposal.</summary>
    private static async Task<NativeAccountResult[]> ContendAsync(NativeBrowserParFixture flow, IServiceProvider peerRoot, NativeCommitGate gate)
    {
        var token = TestContext.Current.CancellationToken;
        var winner = Task.Run(() => flow.PushAsync(), token);
        Task<NativeAccountResult>? peer = null;
        ExceptionDispatchInfo? failure = null;
        try
        {
            var blocker = await gate.WaitAsync(winner, token);
            peer = Task.Run(() => flow.PushAsync(services: peerRoot), token);
            await DatabaseWaiters.ObserveOwnedAsync(flow.Journey.Lease, blocker, ProfileWait, OnePeer, token);
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
}
