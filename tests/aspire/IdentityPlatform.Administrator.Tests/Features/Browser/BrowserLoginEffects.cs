using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Requires independently committed source ceremonies, generation replacement and ticket effects after real browser navigation.</summary>
internal static class BrowserLoginEffects
{
    private const int TwoLogins = 2;
    private const string PasswordEvent = "administrator-password";
    private const string FactorEvent = "administrator-factor";
    private const string Accepted = "accepted";

    /// <summary>Checks both attached native ceremonies and their distinct sources, exact local revoke, generation and replacement audit.</summary>
    internal static async Task ReplacementAsync(BrowserHttpsFixture fixture, string previous)
    {
        await using var issuerScope = fixture.Issuer.Account.Journey.CreateScope();
        var issuer = issuerScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var continuations = await issuer.Set<IssuerContinuation>().AsNoTracking().ToArrayAsync(token);
        Assert.Equal(TwoLogins, continuations.Length);
        Assert.Equal(TwoLogins, continuations.Select(row => row.SessionId).Distinct().Count());
        Assert.All(continuations, row => Assert.Equal(IssuerContinuationPolicy.Attached, row.State));
        await using var clientScope = fixture.Client.Services.CreateAsyncScope();
        var client = clientScope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        var tickets = await client.Set<AdminSessionTicket>().AsNoTracking().ToArrayAsync(token);
        Assert.Equal(TwoLogins, tickets.Length);
        var previousTickets = tickets.Where(row => row.SourceSession == previous).ToArray();
        var previousTicketsCount = previousTickets.Length;
        Assert.Equal(1, previousTicketsCount);
        var old = previousTickets[0];
        Assert.NotNull(old.RevokedAt);
        var active = tickets.Where(row => row.RevokedAt is null).ToArray();
        var activeCount = active.Length;
        Assert.Equal(1, activeCount);
        var current = active[0];
        var exactSource = continuations.Any(row => row.SessionId == current.SourceSession && row.UserId == current.Subject);
        Assert.True(exactSource);
        var browser = await client.Set<BrowserContext>().AsNoTracking().SingleAsync(token);
        Assert.Equal(TwoLogins, browser.Generation);
        Assert.True(browser.TicketDigest == current.Digest);
        var revoke = await client.Set<AdminSessionAudit>().AsNoTracking().SingleAsync(row => row.Operation == SessionMutation.Revoke, token);
        Assert.True(revoke.Digest == old.Digest && revoke.Revision == old.Revision && revoke.Outcome == SessionMutationOutcome.Committed);
    }

    /// <summary>Counts actual accepted password/factor audits so existing issuer authority cannot be credited as a fresh ceremony.</summary>
    internal static async Task<(int Passwords, int Factors)> CeremoniesAsync(BrowserHttpsFixture fixture)
    {
        await using var scope = fixture.Issuer.Account.Journey.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        return (await database.Set<SecurityAuditIntent>().CountAsync(row => row.Event == PasswordEvent && row.Outcome == Accepted, token),
            await database.Set<SecurityAuditIntent>().CountAsync(row => row.Event == FactorEvent && row.Outcome == Accepted, token));
    }

    /// <summary>Proves no canonical ticket or creation audit exists, independently of cookie or rendered error state.</summary>
    internal static async Task NoTicketAsync(BrowserHttpsFixture fixture)
    {
        await using var scope = fixture.Client.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        Assert.Equal(0, await database.Set<AdminSessionTicket>().CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await database.Set<AdminSessionAudit>().CountAsync(row => row.Operation == SessionMutation.Create,
            TestContext.Current.CancellationToken));
    }
}
