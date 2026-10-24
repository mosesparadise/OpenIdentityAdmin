using System.Net;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Preserves authenticated replacement provenance and permits deliberate recovery from stale B1 cookies without accepting a foreign browser actor.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLoginReplacementTests(BootstrapFixture fixture)
{
    private const string CookiePath = "/";
    private const int ReplacementGeneration = 2;
    /// <summary>Protected replacement revokes the real former ticket and records its authenticated actor in the same committed generation.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_AuthenticCurrentTicket_When_ProtectedReplacementStarts_Then_RevocationAndActorAuditCommitTogether(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        _ = await scenario.StartAsync(await scenario.EntryAsync());
        var ticket = await scenario.EstablishAsync();
        _ = await scenario.StartAsync(await scenario.EntryAsync());
        Assert.Null(scenario.Cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Session]);
        var revoked = await scenario.ReadAsync(db => db.Set<AdminSessionTicket>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken));
        Assert.True(revoked.Digest == ticket.Digest && revoked.RevokedAt is not null);
        var audit = await scenario.ReadAsync(db => db.Set<BrowserSessionAudit>().AsNoTracking().SingleAsync(row => row.Operation == BrowserMutation.Start && row.Generation == ReplacementGeneration, TestContext.Current.CancellationToken));
        Assert.True(audit.Subject == BffSessionFixture.Subject && audit.SourceSession == BffSessionFixture.SourceSession);
        Assert.Equal(0, scenario.Host.Bridge.ParSends);
    }

    /// <summary>A revoked canonical handle has no actor authority but cannot trap its browser outside a deliberate fresh protected login.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_RevokedTicketCookie_When_ProtectedLoginStarts_Then_AnonymousGenerationCanReplaceIt(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        _ = await scenario.StartAsync(await scenario.EntryAsync());
        _ = await scenario.EstablishAsync();
        await scenario.ReadAsync(db => db.Set<AdminSessionTicket>().ExecuteUpdateAsync(set => set.SetProperty(row => row.RevokedAt,
            scenario.Issuer.Account.Journey.Clock.Now.UtcTicks), TestContext.Current.CancellationToken));
        _ = await scenario.StartAsync(await scenario.EntryAsync());
        var audit = await scenario.ReadAsync(db => db.Set<BrowserSessionAudit>().AsNoTracking().SingleAsync(row => row.Operation == BrowserMutation.Start && row.Generation == ReplacementGeneration, TestContext.Current.CancellationToken));
        Assert.Null(audit.Subject);
        Assert.Null(audit.SourceSession);
        Assert.Null(scenario.Cookies.GetCookies(scenario.Profile.ClientUri)[BrowserSessionCookies.Session]);
    }

    /// <summary>An authentic live session from another browser cannot be discarded as stale or attributed to this browser's replacement.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ForeignAuthenticTicket_When_NewBrowserStarts_Then_RequestDeniesWithoutChangingEitherBrowser(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        _ = await scenario.StartAsync(await scenario.EntryAsync());
        var ticket = await scenario.EstablishAsync();
        var cookies = new CookieContainer();
        using var browser = scenario.Host.Browser(cookies);
        var token = await scenario.EntryAsync(browser);
        cookies.Add(scenario.Profile.ClientUri, new Cookie(BrowserSessionCookies.Session, ticket.Handle, CookiePath) { Secure = true });
        var before = await scenario.SnapshotAsync();
        using var response = await scenario.PostAsync(token, browser);
        BrowserLoginTests.RequireDenied(response, HttpStatusCode.BadRequest);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(0, scenario.Host.Bridge.ParSends);
    }
}
