using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Connects product start to a genuinely completed ceremony and a single native challenge with protected reservation provenance.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLoginNativeTests(BootstrapFixture fixture)
{
    private const string AuthorizationDestination = "https://issuer.example.test/connect/authorize";
    private const string NativeCookiePrefix = "__Host-identity-admin-client-state";
    private const string SetCookie = "Set-Cookie";
    /// <summary>One actual native PAR/state is created, all original reservation fields survive native cryptography, and another root cannot resume it again.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_GenuineFreshCeremony_When_ProductResumeRuns_Then_ExactlyOneBoundNativeChallengeIsPublished(string provider)
    {
        var probe = new BrowserLoginNativeProbe();
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider, probe.Configure);
        var intent = await scenario.StartAsync(await scenario.EntryAsync());
        var completion = await scenario.CompleteAsync(intent);
        using var challenge = await scenario.ResumeAsync(completion);
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        Assert.True(challenge.Headers.Location?.GetLeftPart(UriPartial.Path) == AuthorizationDestination);
        var cookies = challenge.Headers.GetValues(SetCookie).ToArray();
        Assert.Single(cookies);
        var nativeCookie = cookies[0].StartsWith(NativeCookiePrefix, StringComparison.Ordinal);
        Assert.True(nativeCookie);
        var row = await scenario.ReadAsync(db => db.Set<BrowserLoginIntent>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(BrowserIntentState.Reserved, row.State);
        Assert.NotNull(row.NativeStateDigest);
        Assert.True(await probe.RequireAsync(scenario, scenario.Host, row, intent, completion));
        var before = await scenario.SnapshotAsync();
        await using var peer = await scenario.PeerAsync();
        using var second = peer.Browser(scenario.Cookies);
        using var replay = await scenario.ResumeAsync(completion, second);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(1, scenario.Host.Bridge.ParSends + peer.Bridge.ParSends);
        Assert.Equal(0, scenario.Host.Bridge.TokenSends + peer.Bridge.TokenSends);
        Assert.Equal(1, await scenario.ReadAsync(db => db.Set<OpenIddictEntityFrameworkCoreToken>().CountAsync(TestContext.Current.CancellationToken)));
    }
}
