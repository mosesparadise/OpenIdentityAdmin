using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.AppHost.Tests.Features.BffSessions;

/// <summary>Exercises real transactional rollback, pre-commit expiry checks, cancellation and unknown commit outcomes.</summary>
public sealed class SessionFailureTests
{
    private const int TwoCreatedAudits = 2;
    private const int ThreeTransitions = 3;
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(3);

    /// <summary>Requires failed audit writes to undo each state mutation and rejects expiry crossed while awaiting audit.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_NativeWriteFailures_When_Mutated_Then_StateAndAuditRemainAtomic(string provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(Deadline);
        var token = deadline.Token;
        await using var harness = await SessionStoreHarness.StartAsync(provider, token);
        await AuditRollbackAsync(harness, token);
        await ExpiryDuringAuditAsync(harness, token);
        await UnknownCommitAsync(harness, token);
    }

    /// <summary>Requires cancellation to roll back an in-flight write and unavailable storage to return only finite failure.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CancellationAndDatabaseLoss_When_StoreIsUsed_Then_NoUsableStateEscapes(string provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(Deadline);
        var token = deadline.Token;
        await using var harness = await SessionStoreHarness.StartAsync(provider, token);
        await using var healthy = harness.Node();
        var handle = await SessionConcurrencyTests.CreateAsync(healthy.Store, token);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token);
        var audit = new AuditFault(() => cancelled.Cancel());
        await using (var interrupted = harness.Node(audit))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => interrupted.Store.RenewAsync(handle, 1,
                BffSessionFixture.Ticket(), cancelled.Token));
        }
        await using (var database = harness.Database())
        {
            Assert.Equal(1, (await database.Set<AdminSessionTicket>().SingleAsync(token)).Revision);
            Assert.Equal(1, await database.Set<AdminSessionAudit>().CountAsync(token));
        }
        await harness.StopDatabaseAsync(token);
        var failed = await healthy.Store.LoadAsync(handle, token);
        Assert.Equal(SessionTicketStatus.Failed, failed.Status);
        Assert.Null(failed.Ticket);
        Assert.Null(failed.Handle);
        Assert.Equal(SessionTicketStatus.Failed, (await healthy.Store.RevokeAsync(handle, token)).Status);
    }

    /// <summary>Independently proves create, renewal and revocation roll back after native state writes when audit insertion fails.</summary>
    private static async Task AuditRollbackAsync(SessionStoreHarness harness, CancellationToken token)
    {
        var failure = new AuditFault(fail: true);
        await using var broken = harness.Node(failure);
        var create = await broken.Store.CreateAsync(BffSessionFixture.Ticket(), BffSessionFixture.Subject, BffSessionFixture.SourceSession,
            BffSessionFixture.Start.Add(BffSessionFixture.Absolute), token);
        Assert.Equal(SessionTicketStatus.Failed, create.Status);
        Assert.Null(create.Handle);
        await using var healthy = harness.Node();
        var handle = await SessionConcurrencyTests.CreateAsync(healthy.Store, token);
        Assert.Equal(SessionTicketStatus.Failed, (await broken.Store.RenewAsync(handle, 1, BffSessionFixture.Ticket(), token)).Status);
        Assert.Equal(SessionTicketStatus.Failed, (await broken.Store.RevokeAsync(handle, token)).Status);
        Assert.Equal(SessionTicketStatus.Available, (await healthy.Store.LoadAsync(handle, token)).Status);
        await using var database = harness.Database();
        var row = await database.Set<AdminSessionTicket>().SingleAsync(token);
        Assert.Equal(1, row.Revision);
        Assert.Null(row.RevokedAt);
        Assert.Equal(1, await database.Set<AdminSessionAudit>().CountAsync(token));
        Assert.Equal(ThreeTransitions, failure.Attempts);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => broken.Store.RevokeAsync(handle, cancelled.Token));
    }

    /// <summary>Advances time only after the native row UPDATE, proving the post-audit check rolls back rather than extending an expired session.</summary>
    private static async Task ExpiryDuringAuditAsync(SessionStoreHarness harness, CancellationToken token)
    {
        await using var database = harness.Database();
        await using var healthy = harness.Node();
        var handle = await SessionConcurrencyTests.CreateAsync(healthy.Store, token);
        var fault = new AuditFault(() => harness.Clock.Set(BffSessionFixture.Start.Add(BffSessionFixture.Idle)));
        await using var delayed = harness.Node(fault);
        Assert.Equal(SessionTicketStatus.Unavailable, (await delayed.Store.RenewAsync(handle, 1, BffSessionFixture.Ticket(), token)).Status);
        database.ChangeTracker.Clear();
        Assert.All(await database.Set<AdminSessionTicket>().ToArrayAsync(token), row => Assert.Equal(1, row.Revision));
        Assert.Equal(TwoCreatedAudits, await database.Set<AdminSessionAudit>().CountAsync(token));
        Assert.Equal(1, fault.Attempts);
        harness.Clock.Set(BffSessionFixture.Start);
    }

    /// <summary>Distinguishes a committed row from a lost response and proves the store neither retries nor returns a handle.</summary>
    private static async Task UnknownCommitAsync(SessionStoreHarness harness, CancellationToken token)
    {
        var fault = new CommitResponseFault();
        await using var node = harness.Node(fault);
        var result = await node.Store.CreateAsync(BffSessionFixture.Ticket(), BffSessionFixture.Subject, BffSessionFixture.SourceSession,
            BffSessionFixture.Start.Add(BffSessionFixture.Absolute), token);
        Assert.Equal(SessionTicketStatus.Failed, result.Status);
        Assert.Null(result.Handle);
        Assert.Null(result.Ticket);
        Assert.Equal(1, fault.Commits);
        await using var database = harness.Database();
        Assert.Equal(ThreeTransitions, await database.Set<AdminSessionTicket>().CountAsync(token));
        Assert.Equal(ThreeTransitions, await database.Set<AdminSessionAudit>().CountAsync(token));
    }
}
