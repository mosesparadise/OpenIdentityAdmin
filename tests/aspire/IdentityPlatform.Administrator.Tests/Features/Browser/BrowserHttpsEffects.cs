using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Reads detached committed local and issuer effects through their separate real runtime stores, independently of navigation.</summary>
internal static class BrowserHttpsEffects
{
    private const string IssuerLogout = "administrator-logout";
    private const string AccountActor = "account";
    private const string AccountTarget = "administrator-account";
    private const string Accepted = "accepted";

    /// <summary>Finds exactly one live canonical ticket and its matching current browser association after native callback.</summary>
    internal static async Task<string> SourceAsync(BrowserHttpsFixture fixture)
    {
        await using var scope = fixture.Client.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        var ticket = await database.Set<AdminSessionTicket>().AsNoTracking().SingleAsync(row => row.RevokedAt == null, TestContext.Current.CancellationToken);
        var browser = await database.Set<BrowserContext>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.True(browser.TicketDigest == ticket.Digest && ticket.Subject == fixture.Issuer.Account.ActorId);
        Assert.Equal(1, await database.Set<AdminSessionAudit>().CountAsync(row => row.Digest == ticket.Digest && row.Operation == SessionMutation.Create,
            TestContext.Current.CancellationToken));
        return ticket.SourceSession;
    }

    /// <summary>Requires the exact source's local revoke/audit and winning browser generation, including the independently persisted remote phase.</summary>
    internal static async Task LocalAsync(BrowserHttpsFixture fixture, string source, BrowserLogoutState? state)
    {
        await using var scope = fixture.Client.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        var token = TestContext.Current.CancellationToken;
        var ticket = await database.Set<AdminSessionTicket>().AsNoTracking().SingleAsync(row => row.SourceSession == source, token);
        var browser = await database.Set<BrowserContext>().AsNoTracking().SingleAsync(token);
        Assert.NotNull(ticket.RevokedAt);
        Assert.Null(browser.TicketDigest);
        var audit = await database.Set<AdminSessionAudit>().AsNoTracking().SingleAsync(row => row.Operation == SessionMutation.Revoke, token);
        Assert.True(audit.Digest == ticket.Digest && audit.Revision == ticket.Revision && audit.Outcome == SessionMutationOutcome.Committed);
        var local = await database.Set<BrowserSessionAudit>().AsNoTracking().SingleAsync(row => row.Operation == BrowserMutation.Logout, token);
        Assert.True(local.Subject == ticket.Subject && local.SourceSession == source && local.Generation == browser.Generation && local.CorrelationId == browser.CorrelationId);
        var rows = await database.Set<BrowserLogoutContinuation>().AsNoTracking().ToArrayAsync(token);
        if (state is null)
        {
            Assert.Empty(rows);
            return;
        }
        var count = rows.Length;
        Assert.Equal(1, count);
        var row = rows[0];
        Assert.Equal(state, row.State);
        Assert.True(row.SourceSession == source && row.Subject == ticket.Subject && row.Generation == browser.Generation &&
            row.BrowserDigest == browser.Digest && row.CorrelationId == browser.CorrelationId);
        var remote = await database.Set<BrowserSessionAudit>().AsNoTracking().Where(item =>
            item.Operation == BrowserMutation.RemotePending || item.Operation == BrowserMutation.Return).ToArrayAsync(token);
        Assert.Equal(1, remote.Count(item => item.Operation == BrowserMutation.RemotePending));
        Assert.Equal(state == BrowserLogoutState.ReturnedUnverified ? 1 : 0, remote.Count(item => item.Operation == BrowserMutation.Return));
        Assert.True(remote.All(item => item.CorrelationId == browser.CorrelationId && item.Generation == browser.Generation &&
            item.Subject == null && item.SourceSession == null));
    }

    /// <summary>Checks exact source revocation and actor-bound audit; neither cookie deletion nor returned state is accepted as this evidence.</summary>
    internal static async Task IssuerAsync(BrowserHttpsFixture fixture, string source, bool revoked)
    {
        await using var scope = fixture.Issuer.Account.Journey.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var rows = await database.Set<AdministratorSession>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(revoked, rows.Single(row => row.PublicId == source).Revoked);
        Assert.True(rows.Where(row => row.PublicId != source).All(row => !row.Revoked));
        var audits = await database.Set<SecurityAuditIntent>().AsNoTracking().Where(row => row.Event == IssuerLogout).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(revoked ? 1 : 0, audits.Length);
        if (revoked)
        {
            Assert.True(audits[0].Outcome == Accepted && audits[0].ActorId == fixture.Issuer.Account.ActorId &&
                audits[0].TargetId == fixture.Issuer.Account.ActorId && audits[0].ActorType == AccountActor && audits[0].TargetType == AccountTarget);
            Assert.NotEqual(Guid.Empty, audits[0].Id);
            Assert.NotEqual(Guid.Empty, audits[0].CorrelationId);
            Assert.True(audits[0].OccurredAt > 0 && audits[0].OccurredAt <= fixture.Issuer.Account.Journey.Clock.GetUtcNow().ToUnixTimeSeconds());
        }
    }
}
