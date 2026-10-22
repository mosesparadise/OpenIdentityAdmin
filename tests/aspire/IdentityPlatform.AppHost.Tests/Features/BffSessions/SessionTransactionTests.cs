using System.Runtime.ExceptionServices;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.AppHost.Tests.Features.BffSessions;

/// <summary>Proves shared Sessions transaction participation using native provider writes and independently reopened readers.</summary>
public sealed class SessionTransactionTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(3);

    /// <summary>Commits ticket, audit and an independent native token marker together, while disposal and later owner failure undo all writes.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_SharedSessionTransaction_When_OwnerCompletesOrFails_Then_AllRowsFollowItsOutcome(string provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(Deadline);
        var token = deadline.Token;
        await using var harness = await SessionStoreHarness.StartAsync(provider, token);
        await CompleteAsync(harness, commit: true, token);
        await CompleteAsync(harness, commit: false, token);
        await OwnerFailureAsync(harness, token);
    }

    /// <summary>Rejects commit after failed audit, a missed CAS, cancellation or expiry after staging, even if mutable metadata is extended.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_StagedTicket_When_StageOrFinalAdmissionFails_Then_CommitIsPermanentlyDenied(string provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(Deadline);
        var token = deadline.Token;
        await using var harness = await SessionStoreHarness.StartAsync(provider, token);
        await AuditFailureAsync(harness, token);
        await AdmissionFailureAsync(harness, Failure.Expiry, token);
        await AdmissionFailureAsync(harness, Failure.Cancellation, token);
        await AdmissionFailureAsync(harness, Failure.Revision, token);
    }

    /// <summary>Requires the exact current context transaction, denies reuse, and never retries a native commit with a lost acknowledgement.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_TransactionOwnership_When_ReplacedOrCommitResponseLost_Then_NoSecondCommitIsAllowed(string provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(Deadline);
        var token = deadline.Token;
        await using var harness = await SessionStoreHarness.StartAsync(provider, token);
        await OwnershipAsync(harness, token);
        await OverlapAsync(harness, competingCommit: true, token);
        await OverlapAsync(harness, competingCommit: false, token);
        await UnknownCommitAsync(harness, token);
    }

    /// <summary>Uses the runtime principal to stage a real protected ticket and native marker; an absent revocation must not block legitimate owner work.</summary>
    private static async Task CompleteAsync(SessionStoreHarness harness, bool commit, CancellationToken token)
    {
        var row = Row(harness);
        var marker = Marker();
        await using (var database = harness.Database(runtime: true))
        {
            await using var transaction = await database.Database.BeginTransactionAsync(token);
            var participant = new SessionTicketTransaction(database, transaction, harness.Clock);
            Assert.False(await participant.RevokeAsync(row.Digest, token));
            Assert.True(await participant.CreateAsync(row, token));
            database.Add(marker);
            await database.SaveChangesAsync(token);
            if (commit)
            {
                Assert.True(await participant.CommitAsync(token));
                Assert.False(await participant.CommitAsync(token));
            }
        }
        await AssertStateAsync(harness, row, marker, commit, token);
    }

    /// <summary>Throws a real database constraint failure only after ticket, audit and marker staging, then lets the owner dispose its transaction.</summary>
    private static async Task OwnerFailureAsync(SessionStoreHarness harness, CancellationToken token)
    {
        var row = Row(harness);
        var marker = Marker();
        await using (var database = harness.Database(runtime: true))
        {
            await using var transaction = await database.Database.BeginTransactionAsync(token);
            var participant = new SessionTicketTransaction(database, transaction, harness.Clock);
            Assert.True(await participant.CreateAsync(row, token));
            database.Add(marker);
            await database.SaveChangesAsync(token);
            var invalid = Row(harness);
            invalid.Revision = 0;
            database.Add(invalid);
            await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync(token));
        }
        await AssertStateAsync(harness, row, marker, present: false, token);
    }

    /// <summary>Catches an audit exception after the actual ticket insertion and proves the participant still refuses commit.</summary>
    private static async Task AuditFailureAsync(SessionStoreHarness harness, CancellationToken token)
    {
        var row = Row(harness);
        var marker = Marker();
        var fault = new AuditFault(fail: true);
        await using (var database = harness.Database(runtime: true, fault))
        {
            await using var transaction = await database.Database.BeginTransactionAsync(token);
            database.Add(marker);
            await database.SaveChangesAsync(token);
            var participant = new SessionTicketTransaction(database, transaction, harness.Clock);
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => participant.CreateAsync(row, token));
            Assert.IsType<InvalidOperationException>(error.InnerException);
            Assert.Equal(1, fault.Attempts);
            Assert.False(await participant.CommitAsync(token));
        }
        await AssertStateAsync(harness, row, marker, present: false, token);
    }

    /// <summary>Checks immutable expiry, cancellation and failed predicate poisoning after successful staging and before the one allowed commit.</summary>
    private static async Task AdmissionFailureAsync(SessionStoreHarness harness, Failure failure, CancellationToken token)
    {
        var row = Row(harness);
        var marker = Marker();
        await using (var database = harness.Database(runtime: true))
        {
            await using var transaction = await database.Database.BeginTransactionAsync(token);
            var participant = new SessionTicketTransaction(database, transaction, harness.Clock);
            Assert.True(await participant.CreateAsync(row, token));
            database.Add(marker);
            await database.SaveChangesAsync(token);
            if (failure == Failure.Expiry)
            {
                harness.Clock.Set(new DateTimeOffset(row.IdleExpiresAt, TimeSpan.Zero));
                row.IdleExpiresAt = row.AbsoluteExpiresAt;
                Assert.False(await participant.CommitAsync(token));
                harness.Clock.Set(BffSessionFixture.Start);
            }
            else if (failure == Failure.Cancellation)
            {
                using var cancelled = new CancellationTokenSource();
                cancelled.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => participant.CommitAsync(cancelled.Token));
            }
            else
            {
                var missing = Row(harness);
                Assert.False(await participant.RenewAsync(missing, missing, token));
            }
            Assert.False(await participant.CommitAsync(token));
        }
        await AssertStateAsync(harness, row, marker, present: false, token);
    }

    /// <summary>Rejects an unrelated context, a disposed native transaction, and replacing the original transaction on the same context.</summary>
    private static async Task OwnershipAsync(SessionStoreHarness harness, CancellationToken token)
    {
        await using var database = harness.Database(runtime: true);
        await using var other = harness.Database(runtime: true);
        var transaction = await database.Database.BeginTransactionAsync(token);
        Assert.Throws<InvalidOperationException>(() => new SessionTicketTransaction(other, transaction, harness.Clock));
        var disposed = new SessionTicketTransaction(database, transaction, harness.Clock);
        var replaced = new SessionTicketTransaction(database, transaction, harness.Clock);
        await transaction.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => disposed.CommitAsync(token));
        await using var replacement = await database.Database.BeginTransactionAsync(token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => replaced.CreateAsync(Row(harness), token));
        Assert.False(await replaced.CommitAsync(token));
    }

    /// <summary>Independently observes all rows committed despite a response fault, while the participant refuses a second native attempt.</summary>
    private static async Task UnknownCommitAsync(SessionStoreHarness harness, CancellationToken token)
    {
        var row = Row(harness);
        var marker = Marker();
        var fault = new CommitResponseFault();
        await using (var database = harness.Database(runtime: true, fault))
        {
            await using var transaction = await database.Database.BeginTransactionAsync(token);
            var participant = new SessionTicketTransaction(database, transaction, harness.Clock);
            Assert.True(await participant.CreateAsync(row, token));
            database.Add(marker);
            await database.SaveChangesAsync(token);
            await Assert.ThrowsAsync<InvalidOperationException>(() => participant.CommitAsync(token));
            Assert.False(await participant.CommitAsync(token));
            Assert.Equal(1, fault.Commits);
        }
        await AssertStateAsync(harness, row, marker, present: true, token);
    }

    /// <summary>Holds an actual audit command while a conflicting stage or commit poisons the owner; releasing the first write must never revive commit.</summary>
    private static async Task OverlapAsync(SessionStoreHarness harness, bool competingCommit, CancellationToken token)
    {
        var row = Row(harness);
        var marker = Marker();
        var barrier = new SessionAuditBarrier();
        await using (var database = harness.Database(runtime: true, barrier))
        {
            await using var transaction = await database.Database.BeginTransactionAsync(token);
            database.Add(marker);
            await database.SaveChangesAsync(token);
            var participant = new SessionTicketTransaction(database, transaction, harness.Clock);
            var pending = participant.CreateAsync(row, token);
            ExceptionDispatchInfo? failure = null;
            try
            {
                await barrier.Entered.WaitAsync(token);
                if (competingCommit)
                {
                    Assert.False(await participant.CommitAsync(token));
                }
                else
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => participant.RevokeAsync(row.Digest, token));
                }
            }
            catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
            finally { await barrier.CompleteAsync(pending, failure); }
            Assert.False(await pending);
            Assert.False(await participant.CommitAsync(token));
        }
        await AssertStateAsync(harness, row, marker, present: false, token);
    }

    /// <summary>Builds actual protected B1 material without exposing a browser handle before commit.</summary>
    private static AdminSessionTicket Row(SessionStoreHarness harness)
    {
        var row = BffSessionFixture.Row();
        using var protection = harness.Material.Protection();
        row.ProtectedTicket = new SessionTicketCodec(protection.Provider).Protect(row, BffSessionFixture.Ticket());
        return row;
    }

    /// <summary>Provides an independent native mapped row as a transaction marker, not a substitute for B2 durable redemption.</summary>
    private static OpenIddictEntityFrameworkCoreToken Marker() => new()
    {
        CreationDate = BffSessionFixture.Start.UtcDateTime,
        ExpirationDate = BffSessionFixture.Start.Add(BffSessionFixture.Idle).UtcDateTime,
        Status = OpenIddictConstants.Statuses.Valid,
        Type = OpenIddictConstants.TokenTypeIdentifiers.Private.StateToken
    };

    /// <summary>Reads each committed effect from a fresh runtime context so tracked state cannot satisfy the durability assertions.</summary>
    private static async Task AssertStateAsync(SessionStoreHarness harness, AdminSessionTicket row,
        OpenIddictEntityFrameworkCoreToken marker, bool present, CancellationToken token)
    {
        await using var database = harness.Database(runtime: true);
        Assert.Equal(present, await database.Set<AdminSessionTicket>().AnyAsync(item => item.Digest == row.Digest, token));
        Assert.Equal(present, await database.Set<AdminSessionAudit>().AnyAsync(item => item.Digest == row.Digest, token));
        Assert.Equal(present, await database.Set<OpenIddictEntityFrameworkCoreToken>().AnyAsync(item => item.Id == marker.Id, token));
    }

    /// <summary>Names independent final-admission defects exercised after native staging.</summary>
    private enum Failure
    {
        Expiry, Cancellation, Revision
    }
}
