using System.Net;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises real original-proof publication and genuine correlation retention rather than seeded callback substitutes.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserRemoteLogoutLifetimeTests(BootstrapFixture fixture)
{
    private const string CookiePath = "/";
    private const string SetCookie = "Set-Cookie";
    private const string DeletedCookieValue = "=;";
    /// <summary>A genuine retained pre-callback proof expires before the live ticket and new logout reservation; remote cleanup must not publish a replacement proof.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_OriginalProofExpiresDuringRemoteCleanup_When_LocalLogoutIsKnown_Then_LocalTextSurvivesWithoutPositiveCookieOrLink(string provider)
    {
        await using var scenario = await BrowserRemoteLogoutFixture.CreateAsync(fixture, provider);
        scenario.Login.Cookies.Add(scenario.Login.Profile.ClientUri, new Cookie(BrowserSessionCookies.Generation, scenario.Issuer.PreCallbackGeneration, CookiePath) { Secure = true });
        using var protection = scenario.Login.ReloadProtection();
        var original = new BrowserLoginProof(protection.Provider).Generation(
            scenario.Login.Cookies.GetCookies(scenario.Login.Profile.ClientUri)[BrowserSessionCookies.Generation]!.Value, scenario.Login.Issuer.Account.Journey.Clock.GetUtcNow())!;
        scenario.Login.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(original.Expires, TimeSpan.Zero).AddSeconds(-1);
        var fault = new BrowserRemoteLogoutFault(BrowserMutation.RemotePending)
        {
            AfterCleanup = () => scenario.Login.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(original.Expires, TimeSpan.Zero)
        };
        await using var peer = await scenario.Login.PeerAsync(fault.Configure);
        using var browser = peer.Browser(scenario.Login.Cookies);
        using var response = await scenario.PostAsync(browser);
        await BrowserRemoteLogoutFixture.RequireLocalAsync(response, link: false);
        Assert.Equal(1, fault.Commits);
        Assert.Equal(1, fault.Cleanups);
        var cookies = response.Headers.GetValues(SetCookie).ToArray();
        Assert.True(cookies.All(value => value.Contains(DeletedCookieValue, StringComparison.Ordinal)));
        await scenario.RequireContinuationAsync(BrowserLogoutState.RemotePending);
    }

    /// <summary>Cleanup retains a genuine correlation at equality, deletes it only afterward and never turns its saved cookie into renewed authority.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_GenuineAbandonedCorrelation_When_RetentionBoundaryPasses_Then_CleanupReclaimsItAndSavedReturnStillDenies(string provider)
    {
        await using var scenario = await BrowserRemoteLogoutFixture.CreateAsync(fixture, provider);
        await scenario.BeginAsync();
        var row = await scenario.RequireContinuationAsync(BrowserLogoutState.RemotePending);
        var cookies = scenario.Login.Cookies.GetCookieHeader(scenario.Login.Profile.ClientUri);
        scenario.Login.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(row.RetainUntil, TimeSpan.Zero);
        _ = await scenario.Login.ReadAsync(db => new BrowserSessionCleanup(db, scenario.Login.Issuer.Account.Journey.Clock).RunAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await scenario.Login.ReadAsync(db => db.Set<BrowserLogoutContinuation>().CountAsync(TestContext.Current.CancellationToken)));
        scenario.Login.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(row.RetainUntil + 1, TimeSpan.Zero);
        _ = await scenario.Login.ReadAsync(db => new BrowserSessionCleanup(db, scenario.Login.Issuer.Account.Journey.Clock).RunAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await scenario.Login.ReadAsync(db => db.Set<BrowserLogoutContinuation>().CountAsync(TestContext.Current.CancellationToken)));
        var before = await scenario.Login.SnapshotAsync();
        using var denied = await BrowserCallbackTests.ReplayAsync(scenario.Login.Host, scenario.Return, cookies);
        BrowserLoginTests.RequireDenied(denied, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.Login.SnapshotAsync());
        Assert.Equal(1, await scenario.Login.ReadAsync(db => db.Set<BrowserSessionAudit>().CountAsync(item => item.Operation == BrowserMutation.Logout, TestContext.Current.CancellationToken)));
    }
}
