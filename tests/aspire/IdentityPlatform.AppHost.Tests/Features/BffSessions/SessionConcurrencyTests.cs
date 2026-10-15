using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Testing;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.AppHost.Tests.Features.BffSessions;

/// <summary>Exercises deterministic renewal/revocation interleavings and atomic failures on both actual databases.</summary>
public sealed class SessionConcurrencyTests
{
    private const int RenewedRevision = 2;
    private const int CompleteLifecycleAudits = 3;
    private const int TwoLifecycleAudits = 5;
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(3);

    /// <summary>Proves two authenticated stale writers cannot both commit and revocation wins before or after renewal.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_TwoInstances_When_UpdatesRace_Then_OnlyOneRevisionCommitsAndRevocationWins(string provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(Deadline);
        await using var harness = await SessionStoreHarness.StartAsync(provider, deadline.Token);
        await CompetingRenewalsAsync(harness, deadline.Token);
        await RevokeBeforeRenewAsync(harness, deadline.Token);
    }

    /// <summary>Synchronizes both updates after reading revision one, then proves one audit and permanent later revocation.</summary>
    private static async Task CompetingRenewalsAsync(SessionStoreHarness harness, CancellationToken token)
    {
        const int Writers = 2;
        var barrier = new TicketWriteBarrier(Writers);
        await using var first = harness.Node(barrier);
        await using var second = harness.Node(barrier);
        var handle = await CreateAsync(first.Store, token);
        await using var held = new HeldRenewals(barrier, token);
        var firstRenewal = held.Start(first.Store, handle);
        var secondRenewal = held.Start(second.Store, handle);
        await barrier.Entered.WaitAsync(token);
        barrier.Release();
        var results = await Task.WhenAll(firstRenewal, secondRenewal);
        Assert.Single(results, result => result.Status == SessionTicketStatus.Available);
        Assert.Single(results, result => result.Status == SessionTicketStatus.Unavailable);
        Assert.Equal(SessionTicketStatus.Available, (await second.Store.RevokeAsync(handle, token)).Status);
        Assert.Equal(SessionTicketStatus.Unavailable, (await first.Store.LoadAsync(handle, token)).Status);
        Assert.Equal(SessionTicketStatus.Unavailable, (await first.Store.RenewAsync(handle, Writers, BffSessionFixture.Ticket(), token)).Status);
        await using var database = harness.Database();
        var row = await database.Set<AdminSessionTicket>().SingleAsync(token);
        Assert.Equal(RenewedRevision, row.Revision);
        Assert.NotNull(row.RevokedAt);
        Assert.Equal(CompleteLifecycleAudits, await database.Set<AdminSessionAudit>().CountAsync(token));
    }

    /// <summary>Commits revocation after stale renewal authentication but before its UPDATE, then proves cleanup cannot permit resurrection.</summary>
    private static async Task RevokeBeforeRenewAsync(SessionStoreHarness harness, CancellationToken token)
    {
        var barrier = new TicketWriteBarrier(1);
        await using var stale = harness.Node(barrier);
        await using var revoker = harness.Node();
        var handle = await CreateAsync(stale.Store, token);
        await using var held = new HeldRenewals(barrier, token);
        var renewal = held.Start(stale.Store, handle);
        await barrier.Entered.WaitAsync(token);
        Assert.Equal(SessionTicketStatus.Available, (await revoker.Store.RevokeAsync(handle, token)).Status);
        Assert.Equal(SessionTicketStatus.Available, (await revoker.Store.CleanupAsync(token)).Status);
        barrier.Release();
        Assert.Equal(SessionTicketStatus.Unavailable, (await renewal).Status);
        Assert.Equal(SessionTicketStatus.Unavailable, (await stale.Store.LoadAsync(handle, token)).Status);
        await using var database = harness.Database();
        Assert.Empty(await database.Set<AdminSessionTicket>().ToArrayAsync(token));
        Assert.Equal(TwoLifecycleAudits, await database.Set<AdminSessionAudit>().CountAsync(token));
    }

    /// <summary>Creates one real ticket and asserts successful commitment before returning its private test-only reference.</summary>
    internal static async Task<string> CreateAsync(SessionTicketStore store, CancellationToken token)
    {
        var result = await store.CreateAsync(BffSessionFixture.Ticket(), BffSessionFixture.Subject, BffSessionFixture.SourceSession,
            BffSessionFixture.Start.Add(BffSessionFixture.Absolute), token, BffSessionFixture.Idle);
        Assert.Equal(SessionTicketStatus.Available, result.Status);
        return Assert.IsType<string>(result.Handle);
    }
}
