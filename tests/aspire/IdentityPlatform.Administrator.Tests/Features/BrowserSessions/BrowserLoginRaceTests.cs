using System.Net;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires provider-observed contention across independent product hosts before permitting one native challenge.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLoginRaceTests(BootstrapFixture fixture)
{
    /// <summary>A real blocked peer loses the single reservation and cannot create a second PAR, state or cookie.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_TwoIndependentHosts_When_ValidResumeContends_Then_OnlyOneNativeChallengeIsPublished(string provider)
    {
        var winner = new NativeClientCallbackRace(typeof(BrowserSessionBudget));
        var peer = new NativeClientCallbackRace(typeof(BrowserSessionBudget));
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider, winner.Configure);
        var intent = await scenario.StartAsync(await scenario.EntryAsync());
        var completion = await scenario.CompleteAsync(intent);
        await using var second = await scenario.PeerAsync(peer.Configure);
        using var browser = second.Browser(scenario.Cookies);
        var locked = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        winner.Locked = locked;
        winner.Release = release.Task;
        peer.BeforeLock = waiting;
        var responses = await NativeBlockedRace.InvokeAsync(() => scenario.ResumeAsync(completion), locked.Task,
            () => scenario.ResumeAsync(completion, browser), waiting.Task, release, scenario.Database.ObserveAsync);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);
        BrowserLoginTests.RequireDenied(secondResponse, HttpStatusCode.BadRequest);
        Assert.Equal(1, scenario.Host.Bridge.ParSends + second.Bridge.ParSends);
        Assert.Equal(0, scenario.Host.Bridge.TokenSends + second.Bridge.TokenSends);
        Assert.Equal(1, await scenario.ReadAsync(db => db.Set<OpenIddictEntityFrameworkCoreToken>().CountAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(1, await scenario.ReadAsync(db => db.Set<BrowserSessionAudit>().CountAsync(row => row.Operation == BrowserMutation.Reserve, TestContext.Current.CancellationToken)));
        var before = await scenario.SnapshotAsync();
        using var replay = await scenario.ResumeAsync(completion);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(1, scenario.Host.Bridge.ParSends + second.Bridge.ParSends);
    }
}
