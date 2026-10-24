using System.Net;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises actual provider-observed duplicate callbacks through independent product roots.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserCallbackRaceTests(BootstrapFixture fixture)
{

    /// <summary>A blocked duplicate consumes no second exchange or ticket; only the native-state winner can establish the browser generation.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ConcurrentProductCallbacks_When_NativeStateLockContends_Then_ExactlyOneExchangeAndTicketCommit(string provider)
    {
        var winner = new NativeClientCallbackRace();
        var loser = new NativeClientCallbackRace();
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider, winner.Configure);
        var callback = await scenario.CallbackAsync();
        var cookies = scenario.Cookies.GetCookieHeader(callback);
        await using var peer = await scenario.PeerAsync(loser.Configure);
        var locked = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        winner.Locked = locked;
        winner.Release = release.Task;
        loser.BeforeLock = waiting;
        var results = await NativeBlockedRace.InvokeAsync(() => BrowserCallbackTests.ReplayAsync(scenario.Host, callback, cookies), locked.Task,
            () => BrowserCallbackTests.ReplayAsync(peer, callback, cookies), waiting.Task, release, scenario.Database.ObserveAsync);
        using var accepted = results[0];
        using var rejected = results[1];
        Assert.Equal(HttpStatusCode.SeeOther, accepted.StatusCode);
        BrowserLoginTests.RequireDenied(rejected, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(1, scenario.Host.Bridge.TokenSends + peer.Bridge.TokenSends);
        Assert.Equal(1, await scenario.ReadAsync(db => db.Set<AdminSessionTicket>().CountAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(1, await scenario.ReadAsync(db => db.Set<BrowserSessionAudit>().CountAsync(row => row.Operation == BrowserMutation.Establish, TestContext.Current.CancellationToken)));
    }
}
