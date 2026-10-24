using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Checks original proof deadlines and actual local/native root cleanup before output publication.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLoginDisposalTests(BootstrapFixture fixture)
{
    private const string CleanupFailure = "Synthetic browser cleanup failure.";
    /// <summary>Local cleanup failure after a committed start cannot publish a redirect or cookies despite durable generation advancement.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_LocalDisposalFailure_When_StartHasCommitted_Then_NoSuccessMaterialIsPublished(string provider)
    {
        var probe = new BrowserLoginDisposalProbe(false, () => throw new InvalidOperationException(CleanupFailure));
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider, probe.Configure);
        var token = await scenario.EntryAsync();
        probe.Armed = true;
        using var response = await scenario.PostAsync(token);
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(1, probe.Calls);
        Assert.Equal(1, await scenario.ReadAsync(db => db.Set<BrowserLoginIntent>().CountAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(0, scenario.Host.Bridge.ParSends);
    }

    /// <summary>The new intent deadline cannot replace the old admission-proof deadline during a protected replacement POST.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_OriginalGenerationProofExpiresDuringDisposal_When_ReplacementCommits_Then_NewLifetimeDoesNotAdmitPublication(string provider)
    {
        Action? advance = null;
        var probe = new BrowserLoginDisposalProbe(false, () => advance!());
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider, probe.Configure);
        _ = await scenario.StartAsync(await scenario.EntryAsync());
        var first = await scenario.ReadAsync(db => db.Set<BrowserLoginIntent>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
        var token = await scenario.EntryAsync();
        scenario.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(first.ExpiresAt, TimeSpan.Zero).AddSeconds(-1);
        advance = () => scenario.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(first.ExpiresAt, TimeSpan.Zero);
        probe.Armed = true;
        using var response = await scenario.PostAsync(token);
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(1, probe.Calls);
        var pending = await scenario.ReadAsync(db => db.Set<BrowserLoginIntent>().AsNoTracking().SingleAsync(row => row.State == BrowserIntentState.Pending, TestContext.Current.CancellationToken));
        Assert.Equal(2, pending.Generation);
        Assert.True(pending.ExpiresAt > first.ExpiresAt);
        Assert.Equal(0, scenario.Host.Bridge.ParSends);
    }

    /// <summary>Native root disposal crossing original intent expiry rejects already-staged native cookies/redirect after the real PAR succeeds.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_OriginalIntentExpiresDuringNativeDisposal_When_ResumeCompletes_Then_StagedNativeResponseIsDiscarded(string provider)
    {
        Action? advance = null;
        var probe = new BrowserLoginDisposalProbe(true, () => advance!());
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider, probe.Configure);
        var intent = await scenario.StartAsync(await scenario.EntryAsync());
        var completion = await scenario.CompleteAsync(intent);
        var row = await scenario.ReadAsync(db => db.Set<BrowserLoginIntent>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
        advance = () => scenario.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(row.ExpiresAt, TimeSpan.Zero);
        probe.Armed = true;
        using var response = await scenario.ResumeAsync(completion);
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, probe.Calls);
        Assert.Equal(1, scenario.Host.Bridge.ParSends);
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
        var before = await scenario.SnapshotAsync();
        using var replay = await scenario.ResumeAsync(completion);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(1, scenario.Host.Bridge.ParSends);
    }
}
