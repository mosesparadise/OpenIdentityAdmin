using System.Data;
using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Owns cancellation, ordered locks and atomic audit; disposing an uncommitted ceremony rolls it back.</summary>
internal sealed class CeremonyTransaction : IAsyncDisposable
{
    private readonly PlatformDbContext database;
    private readonly IDbContextTransaction transaction;
    private readonly IDisposable native;
    private readonly CancellationTokenSource deadline;
    private bool completed;
    private bool disposed;
    private readonly CeremonyConnection connection;
    private readonly CeremonyMode mode;
    internal CeremonyLockPlan Plan
    {
        get;
    }
    internal CancellationToken Token => deadline.Token;

    /// <summary>Captures the transaction and operation lease after their lifetime owners have been created.</summary>
    private CeremonyTransaction(PlatformDbContext database, IDbContextTransaction transaction, IDisposable native,
        CancellationTokenSource deadline, CeremonyLockPlan plan, CeremonyConnection connection, CeremonyMode mode)
    {
        this.database = database;
        this.transaction = transaction;
        this.native = native;
        this.deadline = deadline;
        Plan = plan;
        this.connection = connection;
        this.mode = mode;
    }

    /// <summary>Opens one bounded transaction, discards clean stale tracking and acquires the entire declared lock set.</summary>
    internal static Task<CeremonyTransaction> OpenAsync(PlatformDbContext database, NativeOperation operation,
        CeremonyLockPlan plan, CancellationToken cancellationToken) => OpenCoreAsync(database, operation, plan, CeremonyMode.Authentication, cancellationToken);

    /// <summary>Owns only anonymous continuation admission; it cannot commit account or credential work.</summary>
    internal static Task<CeremonyTransaction> OpenContinuationAsync(PlatformDbContext database, NativeOperation operation,
        CancellationToken token) => OpenCoreAsync(database, operation, new CeremonyLockPlan(false, true, [], []), CeremonyMode.Continuation, token);

    /// <summary>Commits only one new continuation and its anonymous audit under the original bounded transaction.</summary>
    internal async Task CommitContinuationAsync(IssuerContinuation row, CeremonyClock clock)
    {
        RequireCurrent();
        var entries = database.ChangeTracker.Entries().ToArray();
        if (mode != CeremonyMode.Continuation || entries is not [var entry] || entry.State != EntityState.Added ||
            !ReferenceEquals(entry.Entity, row) || !IssuerContinuationPolicy.IsStart(row) ||
            !IssuerContinuationPolicy.Observe(row, clock.Now))
        {
            throw new FactorUnavailableException();
        }
        IssuerContinuationAudit.Add(database, row, attached: false);
        await database.SaveChangesAsync(Token);
        RequireContinuationTime(clock);
        await transaction.CommitAsync(Token);
        completed = true;
    }

    /// <summary>Explicitly selects metadata-only cleanup ownership; ordinary ceremonies cannot use its unaudited commit.</summary>
    internal static Task<CeremonyTransaction> OpenMaintenanceAsync(PlatformDbContext database, NativeOperation operation,
        CeremonyLockPlan plan, CancellationToken cancellationToken) => OpenCoreAsync(database, operation, plan, CeremonyMode.Maintenance, cancellationToken);

    /// <summary>Opens governance ownership; authentication and maintenance commits cannot complete this transaction.</summary>
    internal static Task<CeremonyTransaction> OpenGovernanceAsync(PlatformDbContext database, NativeOperation operation,
        CeremonyLockPlan plan, CancellationToken cancellationToken) => OpenCoreAsync(database, operation, plan, CeremonyMode.Governance, cancellationToken);

    /// <summary>Creates bounded connection and transaction lifetimes and releases every partially acquired resource on failure.</summary>
    private static async Task<CeremonyTransaction> OpenCoreAsync(PlatformDbContext database, NativeOperation operation,
        CeremonyLockPlan plan, CeremonyMode mode, CancellationToken cancellationToken)
    {
        if (database.Database.CurrentTransaction is not null || database.ChangeTracker.HasChanges())
        {
            throw new FactorUnavailableException();
        }
        database.ChangeTracker.Clear();
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(AdministratorPolicy.OperationSeconds));
        var connection = new CeremonyConnection(database);
        IDbContextTransaction? transaction = null;
        IDisposable? native = null;
        try
        {
            await connection.OpenAsync(deadline.Token);
            transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, deadline.Token);
            native = operation.Enter(deadline.Token);
            await new CeremonyLocks(database).AcquireAsync(plan, deadline.Token);
            return new CeremonyTransaction(database, transaction, native, deadline, plan, connection, mode);
        }
        catch
        {
            await ReleaseAsync(database, native, transaction, connection, deadline);
            throw;
        }
    }

    /// <summary>Rejects use outside the original transaction or after commit, disposal or cancellation.</summary>
    internal void RequireCurrent()
    {
        if (completed || database.Database.CurrentTransaction?.TransactionId != transaction.TransactionId)
        {
            throw new FactorUnavailableException();
        }
        Token.ThrowIfCancellationRequested();
    }

    /// <summary>Rejects helper contexts that do not own this exact database transaction.</summary>
    internal void RequireContext(PlatformDbContext current)
    {
        RequireCurrent();
        if (!ReferenceEquals(database, current))
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Excludes metadata maintenance from all authentication capability creation and use.</summary>
    internal void RequireCeremony()
    {
        RequireCurrent();
        if (mode != CeremonyMode.Authentication)
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Allows shared locked account mechanisms while excluding metadata-only maintenance.</summary>
    internal void RequireAccountOperation()
    {
        RequireCurrent();
        if (mode is not (CeremonyMode.Authentication or CeremonyMode.Governance))
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Requires the dedicated governance mode and its singleton serialization boundary.</summary>
    internal void RequireGovernance()
    {
        RequireCurrent();
        if (mode != CeremonyMode.Governance || !Plan.Governance)
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Commits a fixed audit event and all native/product writes before any caller may release authority.</summary>
    internal async Task CommitAsync(CeremonyEvent action, bool accepted, string userId, long now, CeremonyClock? clock = null)
    {
        RequireCurrent();
        if (mode != CeremonyMode.Authentication || !Plan.Users.Contains(userId, StringComparer.Ordinal))
        {
            throw new FactorUnavailableException();
        }
        CeremonyAudit.Add(database, action, accepted, userId, now);
        await database.SaveChangesAsync(Token);
        RequireContinuationTime(clock);
        await transaction.CommitAsync(Token);
        completed = true;
    }

    /// <summary>Checks original continuation expiry again after actual persistence and before commit; no delayed write may revive it.</summary>
    private void RequireContinuationTime(CeremonyClock? clock)
    {
        foreach (var entry in database.ChangeTracker.Entries<IssuerContinuation>())
        {
            if (clock is null || !IssuerContinuationPolicy.IsCurrent(entry.Entity, clock.Now))
            {
                throw new FactorUnavailableException();
            }
        }
    }

    /// <summary>Commits metadata-only maintenance without inventing an authentication or credential-change audit.</summary>
    internal async Task CommitMaintenanceAsync()
    {
        RequireCurrent();
        if (mode != CeremonyMode.Maintenance)
        {
            throw new FactorUnavailableException();
        }
        await database.SaveChangesAsync(Token);
        await transaction.CommitAsync(Token);
        completed = true;
    }

    /// <summary>Commits governance only through a private-factory capability that checks actual state and derives the accepted audit.</summary>
    internal async Task CommitGovernanceAsync(GovernanceCommit capability, CeremonyClock clock)
    {
        RequireGovernance();
        await capability.WriteAsync(database, this, clock.Now);
        await database.SaveChangesAsync(Token);
        capability.RequireFinal(this, clock.Now);
        await transaction.CommitAsync(Token);
        completed = true;
    }

    /// <summary>Persists an actual failed native attempt with its denial audit, retaining no verification or target-side effects.</summary>
    internal async Task CommitGovernanceDenialAsync(GovernanceDenial capability, CeremonyClock clock)
    {
        RequireGovernance();
        await capability.WriteAsync(database, this, clock.Now);
        await database.SaveChangesAsync(Token);
        capability.RequireFinal(this, clock.Now);
        await transaction.CommitAsync(Token);
        completed = true;
    }

    /// <summary>Releases native cancellation and rolls back uncommitted work without retaining stale tracked authority.</summary>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        completed = true;
        await ReleaseAsync(database, native, transaction, connection, deadline);
    }

    /// <summary>Releases every acquired owner even when a preceding rollback or connection restoration fails.</summary>
    internal static async ValueTask ReleaseAsync(PlatformDbContext database, IDisposable? native,
        IAsyncDisposable? transaction, IAsyncDisposable connection, CancellationTokenSource deadline)
    {
        try
        {
            native?.Dispose();
        }
        finally
        {
            try
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
            finally
            {
                try
                {
                    await connection.DisposeAsync();
                }
                finally
                {
                    deadline.Dispose();
                    database.ChangeTracker.Clear();
                }
            }
        }
    }
}

internal enum CeremonyMode
{
    Authentication, Maintenance, Governance, Continuation
}
