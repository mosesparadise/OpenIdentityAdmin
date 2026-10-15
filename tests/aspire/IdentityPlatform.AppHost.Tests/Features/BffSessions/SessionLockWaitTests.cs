using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Testing;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.AppHost.Tests.Features.BffSessions;

/// <summary>Proves a renewal waiting in the real database cannot revive a session after its original deadline.</summary>
public sealed class SessionLockWaitTests
{
    private static readonly TimeSpan DrainDeadline = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(3);

    /// <summary>Observes a native lock wait before advancing time to the old deadline, then requires rollback with unchanged audit.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_RenewalBlockedOnNativeLock_When_OriginalIdleDeadlinePasses_Then_UpdateAndAuditRollBack(string provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(Deadline);
        await using var harness = await SessionStoreHarness.StartAsync(provider, deadline.Token);
        var observation = new NativeUpdateObservation();
        var barrier = new TicketWriteBarrier(1);
        await using var node = harness.Node(barrier, observation);
        var handle = await SessionConcurrencyTests.CreateAsync(node.Store, deadline.Token);
        await BlockAndExpireAsync(harness, node.Store, observation, barrier, handle, deadline.Token);
        await using var database = harness.Database();
        var row = await database.Set<AdminSessionTicket>().SingleAsync(deadline.Token);
        Assert.Equal(1, row.Revision);
        Assert.Equal(BffSessionFixture.Start.UtcTicks, row.LastActivityAt);
        Assert.Equal(BffSessionFixture.Start.Add(BffSessionFixture.Idle).UtcTicks, row.IdleExpiresAt);
        Assert.Equal(1, await database.Set<AdminSessionAudit>().CountAsync(deadline.Token));
        Assert.Equal(SessionTicketStatus.Unavailable, (await node.Store.LoadAsync(handle, deadline.Token)).Status);
    }

    /// <summary>Owns the blocking transaction and renewal task, releasing/draining both before fixture teardown even after assertion failure.</summary>
    private static async Task BlockAndExpireAsync(SessionStoreHarness harness, SessionTicketStore store,
        NativeUpdateObservation observation, TicketWriteBarrier barrier, string handle, CancellationToken token)
    {
        await using var locker = harness.Database();
        var transaction = await locker.Database.BeginTransactionAsync(token);
        using var renewalLifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task<SessionTicketResult>? renewal = null;
        try
        {
            renewal = store.RenewAsync(handle, 1, BffSessionFixture.Ticket(), renewalLifetime.Token);
            await barrier.Entered.WaitAsync(token);
            await locker.Set<AdminSessionTicket>().ExecuteUpdateAsync(update => update.SetProperty(row => row.LastActivityAt, row => row.LastActivityAt), token);
            barrier.Release();
            await using var observer = harness.Database();
            await observation.RequireBlockedAsync(observer, token);
            harness.Clock.Set(BffSessionFixture.Start.Add(BffSessionFixture.Idle));
            await transaction.RollbackAsync(token);
            Assert.Equal(SessionTicketStatus.Unavailable, (await renewal).Status);
        }
        finally
        {
            await renewalLifetime.CancelAsync();
            barrier.Release();
            await transaction.DisposeAsync();
            if (renewal is not null)
            {
                try
                {
                    await renewal.WaitAsync(DrainDeadline, CancellationToken.None);
                }
                catch (OperationCanceledException) when (renewalLifetime.IsCancellationRequested) { }
            }
        }
    }
}
