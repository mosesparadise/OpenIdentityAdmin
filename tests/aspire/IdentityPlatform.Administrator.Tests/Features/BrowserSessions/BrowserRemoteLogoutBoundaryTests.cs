using System.Net;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Denies forged, foreign, stale and expired returns while preserving exact durable state and a valid original control.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserRemoteLogoutBoundaryTests(BootstrapFixture fixture)
{
    private const int RollbackSeconds = 31;

    private const string StateParameter = "state";
    private const string DuplicateState = "&state=";
    /// <summary>Missing, foreign and wrong-purpose proofs or query substitutions cannot consume the genuine correlation.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_PendingLogout_When_ReturnProofIsSubstituted_Then_NoMutationAndOriginalReturnStillWorks(string provider)
    {
        await using var scenario = await BrowserRemoteLogoutFixture.CreateAsync(fixture, provider);
        await scenario.BeginAsync();
        var cookies = scenario.Login.Cookies.GetCookieHeader(scenario.Login.Profile.ClientUri);
        using var foreign = scenario.Login.Host.Browser(new CookieContainer());
        _ = await scenario.Login.EntryAsync(foreign);
        var before = await scenario.Login.SnapshotAsync();
        using var missing = await foreign.GetAsync(scenario.Return, TestContext.Current.CancellationToken);
        BrowserLoginTests.RequireDenied(missing, HttpStatusCode.BadRequest);
        var generation = scenario.Login.Cookies.GetCookies(scenario.Login.Profile.ClientUri)[BrowserSessionCookies.Generation]!.Value;
        var logout = scenario.Login.Cookies.GetCookies(scenario.Login.Profile.ClientUri)[BrowserSessionCookies.Logout]!.Value;
        using var purpose = await BrowserCallbackTests.ReplayAsync(scenario.Login.Host, scenario.Return, cookies.Replace(logout, generation, StringComparison.Ordinal));
        BrowserLoginTests.RequireDenied(purpose, HttpStatusCode.BadRequest);
        var forged = new Uri(QueryHelpers.AddQueryString(scenario.Return.GetLeftPart(UriPartial.Path), StateParameter, BrowserLoginSelector.CreateSelector()));
        using var invalid = await BrowserCallbackTests.ReplayAsync(scenario.Login.Host, forged, cookies);
        BrowserLoginTests.RequireDenied(invalid, HttpStatusCode.BadRequest);
        using var duplicate = await BrowserCallbackTests.ReplayAsync(scenario.Login.Host, new Uri(scenario.Return.AbsoluteUri + DuplicateState + scenario.Issuer.Fields[StateParameter]), cookies);
        BrowserLoginTests.RequireDenied(duplicate, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.Login.SnapshotAsync());
        using var control = await scenario.Login.Browser.GetAsync(scenario.Return, TestContext.Current.CancellationToken);
        await BrowserRemoteLogoutTests.RequireReturnedAsync(control);
    }

    /// <summary>An intervening protected sign-in wins durable generation ordering; even retained authentic old cookies cannot consume its predecessor's return.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_OldLogoutReturn_When_NewSignInAdvancesGeneration_Then_LaterIntentAndCookiesRemainUntouched(string provider)
    {
        await using var scenario = await BrowserRemoteLogoutFixture.CreateAsync(fixture, provider);
        await scenario.BeginAsync();
        var old = scenario.Login.Cookies.GetCookieHeader(scenario.Login.Profile.ClientUri);
        _ = await scenario.Login.StartAsync(await scenario.Login.EntryAsync());
        var current = scenario.Login.Cookies.GetCookieHeader(scenario.Login.Profile.ClientUri);
        var before = await scenario.Login.SnapshotAsync();
        using var stale = await BrowserCallbackTests.ReplayAsync(scenario.Login.Host, scenario.Return, old);
        BrowserLoginTests.RequireDenied(stale, HttpStatusCode.Conflict);
        using var mixed = await scenario.Login.Browser.GetAsync(scenario.Return, TestContext.Current.CancellationToken);
        BrowserLoginTests.RequireDenied(mixed, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.Login.SnapshotAsync());
        Assert.True(current == scenario.Login.Cookies.GetCookieHeader(scenario.Login.Profile.ClientUri));
    }

    /// <summary>Original expiry equality and rollback deny without renewal; restoring the original time admits the same untouched correlation.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_OriginalCorrelationBounds_When_ReturnIsExpiredOrClockRollsBack_Then_NoMutationOrLifetimeExtension(string provider)
    {
        await using var scenario = await BrowserRemoteLogoutFixture.CreateAsync(fixture, provider);
        await scenario.BeginAsync();
        var row = await scenario.RequireContinuationAsync(BrowserLogoutState.RemotePending);
        var before = await scenario.Login.SnapshotAsync();
        foreach (var instant in new[] { row.ExpiresAt, row.CreatedAt - TimeSpan.FromSeconds(RollbackSeconds).Ticks })
        {
            scenario.Login.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(instant, TimeSpan.Zero);
            using var denied = await scenario.Login.Browser.GetAsync(scenario.Return, TestContext.Current.CancellationToken);
            BrowserLoginTests.RequireDenied(denied, HttpStatusCode.BadRequest);
            Assert.Equal(before, await scenario.Login.SnapshotAsync());
        }
        scenario.Login.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(row.CreatedAt, TimeSpan.Zero);
        using var control = await scenario.Login.Browser.GetAsync(scenario.Return, TestContext.Current.CancellationToken);
        await BrowserRemoteLogoutTests.RequireReturnedAsync(control);
    }
}
