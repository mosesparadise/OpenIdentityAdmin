using System.Data.Common;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Fails an actual native save and delays its disposed transaction while retaining the real worker's failure identity and fresh reconciliation ownership.</summary>
internal sealed class GrantFailureCapture : GrantCleanupProbe, ISaveChangesInterceptor, IDbCommandInterceptor
{
    private const string Revoked = "revoked";
    private const string Governance = "AdministratorGovernance";
    private const string Update = "UPDATE";
    internal static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(30);
    private PlatformDbContext? original;
    private PlatformDbContext? recovery;
    private Guid? originalTransaction;
    private Guid? recoveryTransaction;
    private int rollbacks;
    private int commits;
    private bool saved;
    internal bool Armed
    {
        get; set;
    }
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource<int> RecoveryLock { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Holds only the already-rolled-back exact provider transaction, never a live governance lock.</summary>
    internal GrantFailureCapture() => AfterDisposal = () =>
    {
        Assert.Equal(1, rollbacks);
        Entered.SetResult();
        Release.Task.WaitAsync(Watchdog, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
    };

    /// <summary>Confirms an actual persisted native revocation inside the transaction before throwing a one-shot storage failure.</summary>
    public async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken cancellationToken = default)
    {
        if (!Armed || data.Context is not PlatformDbContext database ||
            !database.ChangeTracker.Entries<OpenIddictEntityFrameworkCoreToken>().Any(row => row.Entity.Status == Revoked))
        {
            return result;
        }
        Armed = false;
        original = database;
        originalTransaction = database.Database.CurrentTransaction!.TransactionId;
        var id = database.ChangeTracker.Entries<OpenIddictEntityFrameworkCoreToken>().Single(row => row.Entity.Status == Revoked).Entity.Id;
        Assert.Equal(Revoked, await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Where(row => row.Id == id).Select(row => row.Status).SingleAsync(cancellationToken));
        saved = true;
        throw new IOException();
    }

    /// <summary>Binds cleanup only after the original native rollback has actually completed and released its locks.</summary>
    public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData data, CancellationToken cancellationToken = default)
    {
        if (ReferenceEquals(original, data.Context) && original is not null)
        {
            Assert.Equal(originalTransaction, data.TransactionId);
            rollbacks++;
            Bind(original, data.TransactionId, transaction);
        }
        return Task.CompletedTask;
    }

    /// <summary>Identifies the real fresh failure recorder only after original cleanup and context disposal, before its shared governance UPDATE.</summary>
    public ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (original is not null && !ReferenceEquals(original, data.Context) &&
            command.CommandText.TrimStart().StartsWith(Update, StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains(Governance, StringComparison.Ordinal))
        {
            Assert.Null(recovery);
            RequireContextDisposed();
            recovery = Assert.IsType<PlatformDbContext>(data.Context);
            recoveryTransaction = recovery.Database.CurrentTransaction!.TransactionId;
            Assert.NotEqual(originalTransaction, recoveryTransaction);
            RecoveryLock.SetResult(NativeLockObservation.Session(command.Connection!));
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Counts only actual fresh stop commits and rejects an original failed transaction commit.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData data, CancellationToken cancellationToken = default)
    {
        if (original is not null)
        {
            Assert.NotSame(original, data.Context);
            Assert.Same(recovery, data.Context);
            Assert.Equal(recoveryTransaction, data.TransactionId);
            commits++;
        }
        return Task.CompletedTask;
    }

    /// <summary>Requires exact rollback, cleanup and fresh-recording ownership after both contenders have joined.</summary>
    internal void Require(bool stopped)
    {
        Assert.True(saved);
        Assert.NotNull(original);
        Assert.NotNull(recovery);
        Assert.Equal(1, rollbacks);
        Assert.Equal(1, Disposals);
        Assert.Equal(stopped ? 1 : 0, commits);
        RequireContextDisposed();
        Assert.IsType<ObjectDisposedException>(Record.Exception(() => _ = recovery.Model));
        original = null;
    }
}
