using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Observes detached real Sessions state without putting tickets, browser proofs or account data into diagnostics.</summary>
internal static class BrowserHttpsState
{
    /// <summary>Finds the sole current ticket after the preceding browser logged out, requiring its exact durable browser association.</summary>
    internal static async Task<string> CurrentSourceAsync(BrowserHttpsFixture fixture)
    {
        await using var scope = fixture.Client.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        var token = TestContext.Current.CancellationToken;
        var ticket = await database.Set<AdminSessionTicket>().AsNoTracking().SingleAsync(row => row.RevokedAt == null, token);
        Assert.Equal(1, await database.Set<BrowserContext>().CountAsync(row => row.TicketDigest == ticket.Digest, token));
        return ticket.SourceSession;
    }

    /// <summary>Requires the original source's exact return and local revocation without selecting or mutating the later browser.</summary>
    internal static async Task ReturnedAsync(BrowserHttpsFixture fixture, string source)
    {
        await using var scope = fixture.Client.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        var token = TestContext.Current.CancellationToken;
        var row = await database.Set<BrowserLogoutContinuation>().AsNoTracking().SingleAsync(item => item.SourceSession == source, token);
        Assert.Equal(BrowserLogoutState.ReturnedUnverified, row.State);
        var ticket = await database.Set<AdminSessionTicket>().AsNoTracking().SingleAsync(item => item.SourceSession == source, token);
        var browser = await database.Set<BrowserContext>().AsNoTracking().SingleAsync(item => item.Digest == row.BrowserDigest, token);
        Assert.NotNull(ticket.RevokedAt);
        Assert.Null(browser.TicketDigest);
        Assert.True(row.Subject == ticket.Subject && row.Generation == browser.Generation && row.CorrelationId == browser.CorrelationId);
        var revoke = await database.Set<AdminSessionAudit>().AsNoTracking().SingleAsync(item => item.Digest == ticket.Digest && item.Operation == SessionMutation.Revoke, token);
        Assert.True(revoke.Revision == ticket.Revision && revoke.Outcome == SessionMutationOutcome.Committed);
        var local = await database.Set<BrowserSessionAudit>().AsNoTracking().SingleAsync(item => item.Operation == BrowserMutation.Logout && item.SourceSession == source, token);
        Assert.True(local.Subject == ticket.Subject && local.Generation == browser.Generation && local.CorrelationId == browser.CorrelationId);
        var remote = await database.Set<BrowserSessionAudit>().AsNoTracking().Where(item => item.CorrelationId == row.CorrelationId &&
            (item.Operation == BrowserMutation.RemotePending || item.Operation == BrowserMutation.Return)).ToArrayAsync(token);
        Assert.Equal(1, remote.Count(item => item.Operation == BrowserMutation.RemotePending));
        Assert.Equal(1, remote.Count(item => item.Operation == BrowserMutation.Return));
        Assert.True(remote.All(item => item.Generation == browser.Generation && item.Subject == null && item.SourceSession == null));
    }

    /// <summary>Hashes all local coordination, ticket and audit effects so denial and rollback cannot hide an unrelated write.</summary>
    internal static async Task<string> SnapshotAsync(BrowserHttpsFixture fixture)
    {
        await using var scope = fixture.Client.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        var token = TestContext.Current.CancellationToken;
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Contexts = await database.Set<BrowserContext>().AsNoTracking().OrderBy(row => row.Digest).ToArrayAsync(token),
            Logouts = await database.Set<BrowserLogoutContinuation>().AsNoTracking().OrderBy(row => row.Digest).ToArrayAsync(token),
            Intents = await database.Set<BrowserLoginIntent>().AsNoTracking().OrderBy(row => row.Digest).ToArrayAsync(token),
            Audits = await database.Set<BrowserSessionAudit>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Budget = await database.Set<BrowserSessionBudget>().AsNoTracking().ToArrayAsync(token),
            Tickets = await database.Set<AdminSessionTicket>().AsNoTracking().OrderBy(row => row.Digest)
                .Select(row => new { Row = row, row.ProtectedTicket, row.RevokedAt }).ToArrayAsync(token),
            TicketAudits = await database.Set<AdminSessionAudit>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token)
        })));
    }

    /// <summary>Reads the genuine original correlation bound after the local transaction and remote preparation have committed.</summary>
    internal static async Task<long> ExpiryAsync(BrowserHttpsFixture fixture)
    {
        await using var scope = fixture.Client.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        return await database.Set<BrowserLogoutContinuation>().Select(row => row.ExpiresAt).SingleAsync(TestContext.Current.CancellationToken);
    }
}
