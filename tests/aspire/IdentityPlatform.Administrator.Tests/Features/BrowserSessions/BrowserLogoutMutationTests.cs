using System.Net;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Uses exact durable effects to distinguish omitted ticket revocation, repeated return consumption and expired-proof publication.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLogoutMutationTests(BootstrapFixture fixture)
{
    private const string RevokeMarker = "browser-local-revocation-bypass";
    private const string ReturnMarker = "browser-return-once-bypass";
    private const string PublicationMarker = "browser-logout-publication-bypass";
    private const string CookiePath = "/";
    private const string SetCookie = "Set-Cookie";
    private const string DeletedCookie = "=;";
    private const string IssuerLink = "id=\"issuer-sign-out\"";
    private const int DuplicateReturns = 2;

    /// <summary>A successful real local logout must revoke the exact prior ticket, not merely hide it behind a different browser generation.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    [Trait("CriticalDecision", "browser-local-revocation")]
    public async Task Given_GenuineAuthenticatedSession_When_LocalLogoutCommits_Then_ExactTicketAndRevokeAuditAreInvalidated(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        using var login = await scenario.Browser.GetAsync(await scenario.CallbackAsync(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, login.StatusCode);
        var original = await scenario.ReadAsync(db => db.Set<AdminSessionTicket>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
        await BrowserSessionStatusTests.RequireAuthenticatedAsync(scenario, original);
        using var logout = await BrowserLogoutHttp.PostAsync(scenario.Browser, await BrowserLogoutHttp.EntryAsync(scenario.Browser));
        await BrowserLogoutHttp.CommittedAsync(logout);
        var ticket = await scenario.ReadAsync(db => db.Set<AdminSessionTicket>().AsNoTracking().SingleAsync(row => row.Digest == original.Digest, TestContext.Current.CancellationToken));
        var bypass = ticket.RevokedAt is null;
        if (bypass)
        {
            Assert.Equal(original.Revision, ticket.Revision);
            Assert.Equal(0, await scenario.ReadAsync(db => db.Set<AdminSessionAudit>().CountAsync(row => row.Operation == SessionMutation.Revoke, TestContext.Current.CancellationToken)));
            var audit = await scenario.ReadAsync(db => db.Set<BrowserSessionAudit>().AsNoTracking().SingleAsync(row => row.Operation == BrowserMutation.Logout, TestContext.Current.CancellationToken));
            Assert.True(audit.Subject == original.Subject && audit.SourceSession == original.SourceSession);
        }
        else
        {
            await BrowserLogoutTests.RequireCommittedAsync(scenario, authenticated: true);
        }
        Assert.False(bypass, RevokeMarker);
    }

    /// <summary>Replaying retained genuine return cookies cannot add a second Return audit or advance an already consumed correlation.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    [Trait("CriticalDecision", "browser-return-once")]
    public async Task Given_ConsumedAuthenticReturn_When_OriginalCookiesReplay_Then_NoSecondReturnCommits(string provider)
    {
        await using var scenario = await BrowserRemoteLogoutFixture.CreateAsync(fixture, provider);
        await scenario.BeginAsync();
        var cookies = scenario.Login.Cookies.GetCookieHeader(scenario.Login.Profile.ClientUri);
        using var first = await scenario.Login.Browser.GetAsync(scenario.Return, TestContext.Current.CancellationToken);
        await BrowserRemoteLogoutTests.RequireReturnedAsync(first);
        var row = await scenario.RequireContinuationAsync(BrowserLogoutState.ReturnedUnverified);
        var before = await scenario.Login.SnapshotAsync();
        using var replay = await BrowserCallbackTests.ReplayAsync(scenario.Login.Host, scenario.Return, cookies);
        var bypass = replay.StatusCode == HttpStatusCode.OK;
        if (bypass)
        {
            await BrowserRemoteLogoutTests.RequireReturnedAsync(replay);
            var changed = await scenario.Login.ReadAsync(db => db.Set<BrowserLogoutContinuation>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
            Assert.True(changed.Revision == row.Revision + 1 && changed.CreatedAt == row.CreatedAt && changed.ExpiresAt == row.ExpiresAt);
            Assert.Equal(DuplicateReturns, await scenario.Login.ReadAsync(db => db.Set<BrowserSessionAudit>().CountAsync(item => item.Operation == BrowserMutation.Return, TestContext.Current.CancellationToken)));
        }
        else
        {
            BrowserLoginTests.RequireDenied(replay, HttpStatusCode.Conflict);
            Assert.Equal(before, await scenario.Login.SnapshotAsync());
        }
        Assert.False((await scenario.Issuer.EffectsAsync()).Sessions[scenario.Issuer.Source]);
        Assert.False(bypass, ReturnMarker);
    }

    /// <summary>A later live tombstone cannot replace the original browser publication deadline after genuine remote commit and cleanup.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    [Trait("CriticalDecision", "browser-logout-publication")]
    public async Task Given_OriginalProofExpiresDuringCleanup_When_RemoteCommitIsKnown_Then_NoPositiveCookieOrLinkIsPublished(string provider)
    {
        await using var scenario = await BrowserRemoteLogoutFixture.CreateAsync(fixture, provider);
        scenario.Login.Cookies.Add(scenario.Login.Profile.ClientUri, new Cookie(BrowserSessionCookies.Generation, scenario.Issuer.PreCallbackGeneration, CookiePath) { Secure = true });
        using var protection = scenario.Login.ReloadProtection();
        var original = new BrowserLoginProof(protection.Provider).Generation(scenario.Issuer.PreCallbackGeneration, scenario.Login.Issuer.Account.Journey.Clock.GetUtcNow())!;
        scenario.Login.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(original.Expires, TimeSpan.Zero).AddSeconds(-1);
        var fault = new BrowserRemoteLogoutFault(BrowserMutation.RemotePending)
        {
            AfterCleanup = () => scenario.Login.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(original.Expires, TimeSpan.Zero)
        };
        await using var peer = await scenario.Login.PeerAsync(fault.Configure);
        using var browser = peer.Browser(scenario.Login.Cookies);
        using var response = await scenario.PostAsync(browser);
        await BrowserRemoteLogoutFixture.RequireLocalAsync(response);
        Assert.Equal(1, fault.Commits);
        Assert.Equal(1, fault.Cleanups);
        await scenario.RequireContinuationAsync(BrowserLogoutState.RemotePending);
        var positive = response.Headers.GetValues(SetCookie).Any(value => !value.Contains(DeletedCookie, StringComparison.Ordinal));
        var link = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Contains(IssuerLink, StringComparison.Ordinal);
        Assert.Equal(positive, link);
        Assert.False(positive && link, PublicationMarker);
    }
}
