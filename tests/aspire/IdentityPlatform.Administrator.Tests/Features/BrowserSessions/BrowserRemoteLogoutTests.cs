using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Demonstrates genuine issuer effects separately from the BFF's deliberately unverified browser-return status.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserRemoteLogoutTests(BootstrapFixture fixture)
{
    private const int ConcurrentReturns = 2;
    private const string UnverifiedText = "Issuer sign-out result is unverified.";
    private const string SetCookie = "Set-Cookie";
    private const string DeletedLogoutCookie = "__Host-identity-admin-logout=;";
    /// <summary>Only the native protected issuer POST revokes the exact source; a valid BFF return still reports an unverified issuer result.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_GenuineSession_When_AssistedLogoutCompletes_Then_BothCommitsAreObservedButReturnDoesNotAttestIssuer(string provider)
    {
        await using var scenario = await BrowserRemoteLogoutFixture.CreateAsync(fixture, provider);
        var old = scenario.Login.Cookies.GetCookieHeader(scenario.Login.Profile.ClientUri);
        await scenario.BeginAsync();
        var before = await scenario.RequireContinuationAsync(BrowserLogoutState.RemotePending);
        Assert.False((await scenario.Issuer.EffectsAsync()).Sessions[scenario.Issuer.Source]);
        using var remote = await scenario.Issuer.PostAsync(await scenario.Issuer.FormAsync());
        await scenario.Issuer.RequireReturnAsync(remote);
        await scenario.Issuer.RequireRevokedAsync();
        Assert.True(remote.Headers.Location == scenario.Return);
        using var returned = await scenario.Login.Browser.GetAsync(remote.Headers.Location, TestContext.Current.CancellationToken);
        await RequireReturnedAsync(returned);
        var after = await scenario.RequireContinuationAsync(BrowserLogoutState.ReturnedUnverified);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
        Assert.Equal(before.ExpiresAt, after.ExpiresAt);
        Assert.Equal(before.RetainUntil, after.RetainUntil);
        await BrowserLogoutHttp.DeniedStatusAsync(scenario.Login.Host, scenario.Login.Profile.ClientUri, old);
        Assert.Equal(1, scenario.Login.Host.Bridge.ParSends);
        Assert.Equal(1, scenario.Login.Host.Bridge.TokenSends);
    }

    /// <summary>A caller can return without visiting the issuer; concurrent duplicates consume once and cannot manufacture a remote success.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_AuthenticReturnWithoutIssuerVisit_When_DuplicateReturnsRace_Then_OneUnverifiedReturnAndNoIssuerRevocation(string provider)
    {
        await using var scenario = await BrowserRemoteLogoutFixture.CreateAsync(fixture, provider);
        await scenario.BeginAsync();
        var cookies = scenario.Login.Cookies.GetCookieHeader(scenario.Login.Profile.ClientUri);
        var responses = await Task.WhenAll(Enumerable.Range(0, ConcurrentReturns).Select(_ => BrowserCallbackTests.ReplayAsync(scenario.Login.Host, scenario.Return, cookies)));
        try
        {
            Assert.Equal(1, responses.Count(item => item.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(item => item.StatusCode == HttpStatusCode.Conflict));
            await RequireReturnedAsync(responses.Single(item => item.StatusCode == HttpStatusCode.OK));
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
        await scenario.RequireContinuationAsync(BrowserLogoutState.ReturnedUnverified);
        Assert.False((await scenario.Issuer.EffectsAsync()).Sessions[scenario.Issuer.Source]);
        Assert.Equal(1, await scenario.Login.ReadAsync(db => db.Set<BrowserSessionAudit>().CountAsync(row => row.Operation == BrowserMutation.Return, TestContext.Current.CancellationToken)));
        var before = await scenario.Login.SnapshotAsync();
        using var replay = await BrowserCallbackTests.ReplayAsync(scenario.Login.Host, scenario.Return, cookies);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.Conflict);
        Assert.Equal(before, await scenario.Login.SnapshotAsync());
    }

    /// <summary>Cancellation at the issuer and a lost navigation leave local revocation intact with no fabricated completion or automatic protocol retry.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_AssistedLocalCommit_When_IssuerConfirmationIsAbandonedOrCookieAbsent_Then_LocalRemainsRevokedAndRemotePending(string provider)
    {
        await using var scenario = await BrowserRemoteLogoutFixture.CreateAsync(fixture, provider);
        var old = scenario.Login.Cookies.GetCookieHeader(scenario.Login.Profile.ClientUri);
        await scenario.BeginAsync();
        _ = await scenario.Issuer.FormAsync();
        using var anonymous = scenario.Issuer.Host.Browser(new CookieContainer());
        using var denied = await scenario.Issuer.GetAsync(browser: anonymous);
        Assert.False(denied.IsSuccessStatusCode);
        await scenario.RequireContinuationAsync(BrowserLogoutState.RemotePending);
        Assert.False((await scenario.Issuer.EffectsAsync()).Sessions[scenario.Issuer.Source]);
        await BrowserLogoutHttp.DeniedStatusAsync(scenario.Login.Host, scenario.Login.Profile.ClientUri, old);
        Assert.Equal(1, scenario.Login.Host.Bridge.ParSends);
        Assert.Equal(1, scenario.Login.Host.Bridge.TokenSends);
    }

    /// <summary>Requires a safe return outcome and permits only the dedicated logout-correlation cookie to expire.</summary>
    internal static async Task RequireReturnedAsync(HttpResponseMessage response)
    {
        await BrowserRemoteLogoutFixture.RequireLocalAsync(response, link: false);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(html.Contains(UnverifiedText, StringComparison.Ordinal));
        var values = response.Headers.GetValues(SetCookie).ToArray();
        var singleCookie = values.Length == 1;
        Assert.True(singleCookie);
        var cookie = values[0];
        var deleted = cookie.StartsWith(DeletedLogoutCookie, StringComparison.Ordinal);
        Assert.True(deleted);
    }
}
