using System.Data.Common;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Distinguishes the original worker/governance transaction from the fresh transaction that durably records an authority stop.</summary>
internal sealed class GrantAuthorityRaceProbe(bool holdCommit) : DbCommandInterceptor, IDbTransactionInterceptor
{
    private const string Governance = "AdministratorGovernance";
    private const string Update = "UPDATE";
    private PlatformDbContext? original;
    private PlatformDbContext? recovery;
    private Guid? originalCommit;
    private Guid? recoveryCommit;
    private int originalCommits;
    private int recoveryCommits;
    internal bool Armed
    {
        get; set;
    }
    internal TaskCompletionSource<int> Lock { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource<int> Committing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Identifies exact native provider sessions before their real governance lock UPDATE, retaining separate failed-operation and recovery owners.</summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Armed && command.CommandText.TrimStart().StartsWith(Update, StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains(Governance, StringComparison.Ordinal))
        {
            var database = Assert.IsType<PlatformDbContext>(data.Context);
            Assert.NotNull(database.Database.CurrentTransaction);
            if (original is null)
            {
                original = database;
                Lock.SetResult(NativeLockObservation.Session(command.Connection!));
            }
            else if (!ReferenceEquals(original, database))
            {
                Assert.Null(recovery);
                recovery = database;
            }
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Holds only the selected original transaction after production checks and writes, never the subsequent failure-recording transaction.</summary>
    public async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData data,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (Armed && ReferenceEquals(original, data.Context))
        {
            Assert.Null(originalCommit);
            originalCommit = data.TransactionId;
            if (holdCommit)
            {
                Committing.SetResult(NativeLockObservation.Session(transaction.Connection!));
                await Release.Task.WaitAsync(cancellationToken);
            }
        }
        else if (Armed && recovery is not null && ReferenceEquals(recovery, data.Context))
        {
            Assert.Null(recoveryCommit);
            recoveryCommit = data.TransactionId;
        }
        return result;
    }

    /// <summary>Counts actual provider commit acknowledgements bound to their exact context and transaction identity.</summary>
    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData data, CancellationToken cancellationToken = default)
    {
        if (Armed && ReferenceEquals(original, data.Context))
        {
            Assert.Equal(originalCommit, data.TransactionId);
            originalCommits++;
        }
        else if (Armed && recovery is not null && ReferenceEquals(recovery, data.Context))
        {
            Assert.Equal(recoveryCommit, data.TransactionId);
            recoveryCommits++;
        }
        return Task.CompletedTask;
    }

    /// <summary>Requires the expected original commit or rollback followed by exactly one fresh stop commit, then disarms later verification operations.</summary>
    internal void Require(bool committed, bool stopped)
    {
        Assert.NotNull(original);
        Assert.Equal(committed ? 1 : 0, originalCommits);
        Assert.Equal(stopped ? 1 : 0, recoveryCommits);
        Assert.Equal(committed, originalCommit is not null);
        Assert.Equal(stopped, recoveryCommit is not null);
        Assert.Equal(stopped, recovery is not null);
        Armed = false;
    }
}
