using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.AppHost.Tests.Features.BffSessions;

/// <summary>Requires bounded physical cleanup to preserve live rows and independent audit intent on both providers.</summary>
public sealed class SessionCleanupTests
{
    private static readonly TimeSpan DrainDeadline = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(3);
    private const int ExpiredRows = 101;
    private const int FirstBatch = 100;

    /// <summary>Creates real protected tickets and proves the batch limit, retained audit and live-state exclusion.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_MoreThanOneBatch_When_Cleaned_Then_LiveTicketsAndAuditSurvive(string provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(Deadline);
        var token = deadline.Token;
        await using var harness = await SessionStoreHarness.StartAsync(provider, token);
        await using var node = harness.Node();
        for (var index = 0; index < ExpiredRows; index++)
        {
            _ = await SessionConcurrencyTests.CreateAsync(node.Store, token);
        }
        harness.Clock.Set(BffSessionFixture.Start.Add(BffSessionFixture.Idle));
        var live = await SessionConcurrencyTests.CreateAsync(node.Store, token);
        var first = await node.Store.CleanupAsync(token);
        Assert.Equal(SessionTicketStatus.Available, first.Status);
        Assert.Equal(FirstBatch, first.Removed);
        Assert.Equal(1, (await node.Store.CleanupAsync(token)).Removed);
        Assert.Equal(0, (await node.Store.CleanupAsync(token)).Removed);
        Assert.Equal(SessionTicketStatus.Available, (await node.Store.LoadAsync(live, token)).Status);
        await using var database = harness.Database();
        Assert.Equal(1, await database.Set<AdminSessionTicket>().CountAsync(token));
        Assert.Equal(102, await database.Set<AdminSessionAudit>().CountAsync(token));
    }
    /// <summary>Requires deletion to recheck a stale candidate after another instance has renewed it into a live window.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_StaleCleanupCandidate_When_AnotherInstanceRenews_Then_DeletePreservesLiveTicket(string provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(Deadline);
        await using var harness = await SessionStoreHarness.StartAsync(provider, deadline.Token);
        await using var writer = harness.Node();
        var handle = await SessionConcurrencyTests.CreateAsync(writer.Store, deadline.Token);
        var barrier = new TicketWriteBarrier(1, deletion: true);
        var cleanupClock = new BffSessionClock(BffSessionFixture.Start.Add(BffSessionFixture.Idle));
        await using var cleaner = harness.Node(cleanupClock, barrier);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
        var cleanup = cleaner.Store.CleanupAsync(lifetime.Token);
        try
        {
            await barrier.Entered.WaitAsync(deadline.Token);
            const int RenewalMinute = 4;
            harness.Clock.Set(BffSessionFixture.Start.AddMinutes(RenewalMinute));
            Assert.Equal(SessionTicketStatus.Available, (await writer.Store.RenewAsync(handle, 1, BffSessionFixture.Ticket(), deadline.Token)).Status);
            barrier.Release();
            Assert.Equal(0, (await cleanup).Removed);
            Assert.Equal(SessionTicketStatus.Available, (await cleaner.Store.LoadAsync(handle, deadline.Token)).Status);
        }
        finally
        {
            await lifetime.CancelAsync();
            barrier.Release();
            try
            {
                await cleanup.WaitAsync(DrainDeadline, CancellationToken.None);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        }
    }
}
