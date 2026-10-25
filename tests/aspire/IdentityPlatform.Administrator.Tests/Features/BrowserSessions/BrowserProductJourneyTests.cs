using System.Net;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises remaining end-to-end product outcomes with genuine issuer ceremonies and both durable providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserProductJourneyTests(BootstrapFixture fixture)
{
    private const int TwoLogins = 2;
    private const string GovernanceLock = "AdministratorGovernanceLocks";

    /// <summary>Both full sign-ins use the same real browser jars; replacement requires another ceremony and revokes the prior local authority.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ExistingProductAndIssuerCookies_When_ExplicitSignInRepeats_Then_NewCeremonyAndGenerationReplaceOldTicket(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        using var first = await scenario.Browser.GetAsync(await scenario.CallbackAsync(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, first.StatusCode);
        var oldHandle = scenario.Cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Session]!.Value;
        Assert.True(SessionHandle.TryDigest(oldHandle, out var oldDigest));
        var oldProof = await scenario.EntryAsync();
        using var second = await scenario.Browser.GetAsync(await scenario.CallbackAsync(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, second.StatusCode);
        var newHandle = scenario.Cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Session]!.Value;
        Assert.True(newHandle != oldHandle && SessionHandle.TryDigest(newHandle, out _));
        await RequireReplacementAsync(scenario, oldDigest);
        var before = await scenario.SnapshotAsync();
        using var stale = await scenario.PostAsync(oldProof);
        BrowserLoginTests.RequireDenied(stale, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(TwoLogins, scenario.Host.Bridge.TokenSends);
        Assert.Equal(TwoLogins, scenario.Host.Bridge.ParSends);
    }

    /// <summary>Inspects independently persisted source sessions and local revocation; two redirects alone are not proof of two ceremonies.</summary>
    private static async Task RequireReplacementAsync(BrowserLoginFixture scenario, string oldDigest)
    {
        await using var scope = scenario.Issuer.Account.Journey.CreateScope();
        var sources = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<IssuerContinuation>()
            .AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(TwoLogins, sources.Length);
        Assert.Equal(TwoLogins, sources.Select(row => row.SessionId).Distinct().Count());
        Assert.All(sources, row => Assert.Equal(IssuerContinuationPolicy.Attached, row.State));
        await scenario.ReadAsync(async database =>
        {
            var tickets = await database.Set<AdminSessionTicket>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Equal(TwoLogins, tickets.Length);
            Assert.NotNull(Assert.Single(tickets, row => row.Digest == oldDigest).RevokedAt);
            var current = Assert.Single(tickets, row => row.RevokedAt is null);
            Assert.Contains(sources, row => row.SessionId == current.SourceSession && row.UserId == current.Subject);
            var browser = await database.Set<BrowserContext>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(TwoLogins, browser.Generation);
            Assert.Equal(current.Digest, browser.TicketDigest);
            return true;
        });
    }

    /// <summary>A supported audited permission loss commits before callback exchange and prevents product establishment without retry.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ValidProductCode_When_AuthorityLossCommitsBeforeExchange_Then_CallbackCreatesNoTicketOrRetry(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var callback = await scenario.CallbackAsync();
        var cookies = scenario.Cookies.GetCookieHeader(callback);
        await using var mutation = await NativeAuthorityMutation.PrepareAsync(scenario.Issuer,
            NativeAuthorityMutation.Change.Permissions, new NativeAuthorityRaceProbe(false, GovernanceLock));
        await mutation.InvokeAsync();
        await mutation.RequirePersistedAsync(scenario.Issuer, NativeAuthorityMutation.Change.Permissions);
        using var denied = await scenario.Browser.GetAsync(callback, TestContext.Current.CancellationToken);
        BrowserLoginTests.RequireDenied(denied, HttpStatusCode.ServiceUnavailable);
        await BrowserCallbackTests.RequireNoTicketAsync(scenario, sends: 1);
        using var replay = await BrowserCallbackTests.ReplayAsync(scenario.Host, callback, cookies);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(1, scenario.Host.Bridge.TokenSends);
    }

    /// <summary>Reads the actual protected native state deadline and retains original callback cookies; an expired callback never reaches transport.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_OriginalProductCallback_When_NativeStateExpiresBeforeArrival_Then_NoExchangeOrTicketIsPublished(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var callback = await scenario.CallbackAsync();
        var cookies = scenario.Cookies.GetCookieHeader(callback);
        var expires = await scenario.ReadAsync(database => database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Where(row => row.Status == OpenIddictConstants.Statuses.Valid).Select(row => row.ExpirationDate).SingleAsync(TestContext.Current.CancellationToken));
        Assert.NotNull(expires);
        scenario.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(DateTime.SpecifyKind(expires.Value, DateTimeKind.Utc));
        using var denied = await BrowserCallbackTests.ReplayAsync(scenario.Host, callback, cookies);
        BrowserLoginTests.RequireDenied(denied, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
        Assert.Equal(0, await scenario.ReadAsync(database => database.Set<AdminSessionTicket>().CountAsync(TestContext.Current.CancellationToken)));
        var before = await scenario.SnapshotAsync();
        using var replay = await BrowserCallbackTests.ReplayAsync(scenario.Host, callback, cookies);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
    }
}
