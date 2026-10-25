using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Reclaims an actual committed but undelivered callback through both owning cleanup services, preserving audit and native evidence.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserOrphanCleanupTests(BootstrapFixture fixture)
{
    private const string CookieSeparator = "; ";
    private const string CookieDelimiter = ",";
    private const string AuthenticatedStatus = "\"authenticated\":true";
    private const int CoordinationRows = 2;

    /// <summary>Canonical ticket expiry and the later actual coordination dependency have independent boundaries; neither cleanup owner deletes audit history.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_UndeliveredGenuineCallback_When_TicketThenCoordinationBoundsExpire_Then_BothOwnersReclaimOnlyEligibleRowsAndPreserveEvidence(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var callback = await scenario.CallbackAsync();
        var original = scenario.Cookies.GetCookieHeader(callback);
        var delivered = new CookieContainer();
        delivered.SetCookies(scenario.Profile.ClientUri, original.Replace(CookieSeparator, CookieDelimiter, StringComparison.Ordinal));
        using var detached = scenario.Host.Browser(delivered);
        using var login = await detached.GetAsync(callback, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, login.StatusCode);
        var issued = delivered.GetCookieHeader(scenario.Profile.ClientUri);
        using var positive = await detached.GetAsync(BrowserLogoutHttp.Status, TestContext.Current.CancellationToken);
        Assert.Contains(AuthenticatedStatus, await positive.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        var bounds = await BoundsAsync(scenario);
        var evidence = await EvidenceAsync(scenario);
        scenario.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(bounds.Idle - 1, TimeSpan.Zero);
        Assert.Equal(0, await CleanTicketAsync(scenario));
        Assert.Equal(0, await CleanCoordinationAsync(scenario));
        scenario.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(bounds.Idle, TimeSpan.Zero);
        Assert.Equal(1, await CleanTicketAsync(scenario));
        Assert.Equal(0, await CleanCoordinationAsync(scenario));
        scenario.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(bounds.Retained, TimeSpan.Zero);
        Assert.Equal(0, await CleanCoordinationAsync(scenario));
        scenario.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(bounds.Retained + 1, TimeSpan.Zero);
        Assert.Equal(CoordinationRows, await CleanCoordinationAsync(scenario));
        Assert.Equal(0, await CleanTicketAsync(scenario));
        Assert.Equal(evidence, await EvidenceAsync(scenario));
        await RequireEmptyAsync(scenario);
        var before = await scenario.SnapshotAsync();
        await BrowserLogoutHttp.DeniedStatusAsync(scenario.Host, scenario.Profile.ClientUri, issued);
        using var replay = await BrowserCallbackTests.ReplayAsync(scenario.Host, callback, original);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(1, scenario.Host.Bridge.TokenSends);
        Assert.Equal(1, scenario.Host.Bridge.ParSends);
    }

    /// <summary>Reads actual committed deadlines and requires both markers to share the latest actual dependency before testing strict equality.</summary>
    private static Task<(long Idle, long Retained)> BoundsAsync(BrowserLoginFixture scenario) => scenario.ReadAsync(async database =>
    {
        var ticket = await database.Set<AdminSessionTicket>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var context = await database.Set<BrowserContext>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        var intent = await database.Set<BrowserLoginIntent>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(BrowserIntentState.Established, intent.State);
        Assert.Equal(context.RetainUntil, intent.RetainUntil);
        Assert.True(context.RetainUntil > ticket.AbsoluteExpiresAt && ticket.AbsoluteExpiresAt > ticket.IdleExpiresAt);
        return (ticket.IdleExpiresAt, context.RetainUntil);
    });

    /// <summary>The B1 owner independently removes expired canonical tickets, which coordination cleanup deliberately does not own.</summary>
    private static Task<int> CleanTicketAsync(BrowserLoginFixture scenario) => scenario.ReadAsync(database =>
        new SessionTicketPersistence(database, scenario.Issuer.Account.Journey.Clock)
            .CleanupAsync(scenario.Issuer.Account.Journey.Clock.GetUtcNow(), TestContext.Current.CancellationToken));

    /// <summary>The browser owner orders its bounded child-first reclamation with all other browser writers.</summary>
    private static Task<int> CleanCoordinationAsync(BrowserLoginFixture scenario) => scenario.ReadAsync(database =>
        new BrowserSessionCleanup(database, scenario.Issuer.Account.Journey.Clock).RunAsync(TestContext.Current.CancellationToken));

    /// <summary>Retains every field of audit and native state without exposing protected payloads or treating another owner's rows as cleanup targets.</summary>
    private static Task<string> EvidenceAsync(BrowserLoginFixture scenario) => scenario.ReadAsync(async database => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        Browser = await database.Set<BrowserSessionAudit>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(TestContext.Current.CancellationToken),
        Tickets = await database.Set<AdminSessionAudit>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(TestContext.Current.CancellationToken),
        Native = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(TestContext.Current.CancellationToken)
    }))));

    /// <summary>Requires fresh empty owner tables rather than relying only on returned deletion counts.</summary>
    private static Task<bool> RequireEmptyAsync(BrowserLoginFixture scenario) => scenario.ReadAsync(async database =>
    {
        Assert.Empty(await database.Set<AdminSessionTicket>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await database.Set<BrowserContext>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await database.Set<BrowserLoginIntent>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken));
        return true;
    });
}
