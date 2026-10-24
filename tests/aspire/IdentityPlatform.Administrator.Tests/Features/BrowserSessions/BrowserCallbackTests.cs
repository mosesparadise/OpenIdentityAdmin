using System.Net;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Proves actual product callbacks establish one canonical committed session across independent service roots.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserCallbackTests(BootstrapFixture fixture)
{
    private const string SessionPrefix = "__Host-identity-admin-session=";
    private const string CookieHeader = "Cookie";

    private const int CanonicalClaims = 2;
    private const string SetCookie = "Set-Cookie";

    /// <summary>Real password/MFA, native authorization and callback yield one protected B1 ticket; original callback replay cannot exchange again.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_FreshProductCeremony_When_IndependentHostCompletesCallback_Then_OneCanonicalTicketAndOpaqueCookieCommit(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var callback = await scenario.CallbackAsync();
        var cookies = scenario.Cookies.GetCookieHeader(callback);
        await using var peer = await scenario.PeerAsync();
        using var browser = peer.Browser(scenario.Cookies);
        using var response = await browser.GetAsync(callback, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal("/bff/login", response.Headers.Location?.OriginalString);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        var session = Assert.Single(response.Headers.GetValues(SetCookie), cookie => cookie.StartsWith(SessionPrefix, StringComparison.Ordinal));
        Assert.Contains("secure", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", session, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", session, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", session, StringComparison.OrdinalIgnoreCase);
        await RequireTicketAsync(scenario);
        var before = await scenario.SnapshotAsync();
        using var replay = await ReplayAsync(scenario.Host, callback, cookies);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(1, peer.Bridge.TokenSends);
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
        _ = await scenario.EntryAsync(browser);
    }

    /// <summary>A real re-signed wrong-intent identity cannot establish a local ticket after native exchange and state consumption.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_GenuineNativeCallback_When_SignedIntentIsSubstituted_Then_SpentStateCreatesNoLocalSession(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var callback = await scenario.CallbackAsync();
        var cookies = scenario.Cookies.GetCookieHeader(callback);
        scenario.Host.Bridge.RewriteBrowserIntent = true;
        using var response = await scenario.Browser.GetAsync(callback, TestContext.Current.CancellationToken);
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(1, scenario.Host.Bridge.IdentityRewrites);
        await RequireNoTicketAsync(scenario, sends: 1);
        using var replay = await ReplayAsync(scenario.Host, callback, cookies);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(1, scenario.Host.Bridge.TokenSends);
    }

    /// <summary>Reads real issuer completion and both local audit streams independently of the HTTP success response.</summary>
    private static async Task RequireTicketAsync(BrowserLoginFixture scenario)
    {
        var handle = scenario.Cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Session]!.Value;
        Assert.True(SessionHandle.TryDigest(handle, out var digest));
        await using var issuerScope = scenario.Issuer.Account.Journey.CreateScope();
        var source = await issuerScope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<IssuerContinuation>()
            .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        await scenario.ReadAsync(async database =>
        {
            var ticket = await database.Set<AdminSessionTicket>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            var browser = await database.Set<BrowserContext>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            var intent = await database.Set<BrowserLoginIntent>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(digest, ticket.Digest);
            Assert.Equal(source.UserId, ticket.Subject);
            Assert.Equal(source.SessionId, ticket.SourceSession);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(source.SessionExpiresAt!.Value).UtcTicks, ticket.AbsoluteExpiresAt);
            Assert.Equal(ticket.Digest, browser.TicketDigest);
            Assert.Equal(ticket.Digest, intent.TicketDigest);
            Assert.Equal(browser.Generation, intent.Generation);
            Assert.Equal(BrowserIntentState.Established, intent.State);
            var canonical = scenario.Host.Services.GetRequiredService<SessionTicketCodec>().Unprotect(ticket)!;
            Assert.Equal(SessionTicketPolicy.Scheme, canonical.AuthenticationScheme);
            Assert.Single(canonical.Principal.Identities);
            Assert.Equal(CanonicalClaims, canonical.Principal.Claims.Count());
            Assert.Empty(canonical.Properties.GetTokens());
            var audit = await database.Set<BrowserSessionAudit>().SingleAsync(row => row.Operation == BrowserMutation.Establish, TestContext.Current.CancellationToken);
            Assert.Equal(ticket.Subject, audit.Subject);
            Assert.Equal(ticket.SourceSession, audit.SourceSession);
            var ticketAudit = Assert.Single(await database.Set<AdminSessionAudit>().ToArrayAsync(TestContext.Current.CancellationToken));
            Assert.Equal(ticket.Digest, ticketAudit.Digest);
            Assert.Equal(ticket.Revision, ticketAudit.Revision);
            Assert.Equal(SessionMutation.Create, ticketAudit.Operation);
            Assert.Equal(SessionMutationOutcome.Committed, ticketAudit.Outcome);
            Assert.Equal(1, await database.Set<OpenIddictEntityFrameworkCoreToken>().CountAsync(row => row.Status == OpenIddictConstants.Statuses.Redeemed, TestContext.Current.CancellationToken));
            return true;
        });
    }

    /// <summary>Requires durable native consumption while the product intent stays spent and no local authentication is created.</summary>
    internal static async Task RequireNoTicketAsync(BrowserLoginFixture scenario, int sends)
    {
        Assert.Equal(sends, scenario.Host.Bridge.TokenSends);
        await scenario.ReadAsync(async database =>
        {
            Assert.Empty(await database.Set<AdminSessionTicket>().ToArrayAsync(TestContext.Current.CancellationToken));
            Assert.Empty(await database.Set<AdminSessionAudit>().ToArrayAsync(TestContext.Current.CancellationToken));
            Assert.False(await database.Set<BrowserSessionAudit>().AnyAsync(row => row.Operation == BrowserMutation.Establish, TestContext.Current.CancellationToken));
            Assert.Equal(BrowserIntentState.Reserved, (await database.Set<BrowserLoginIntent>().SingleAsync(TestContext.Current.CancellationToken)).State);
            Assert.Equal(1, await database.Set<OpenIddictEntityFrameworkCoreToken>().CountAsync(row => row.Status == OpenIddictConstants.Statuses.Redeemed, TestContext.Current.CancellationToken));
            return true;
        });
    }

    /// <summary>Retains the genuine original callback cookies, so replay denial cannot be masked by first-response correlation deletion.</summary>
    internal static async Task<HttpResponseMessage> ReplayAsync(BrowserLoginHost host, Uri callback, string cookies)
    {
        var jar = new CookieContainer();
        using var browser = host.Browser(jar);
        using var request = new HttpRequestMessage(HttpMethod.Get, callback);
        request.Headers.TryAddWithoutValidation(CookieHeader, cookies);
        return await browser.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
