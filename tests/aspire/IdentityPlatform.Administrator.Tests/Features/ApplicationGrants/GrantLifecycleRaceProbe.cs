using System.Data.Common;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Observes one API node's actual governance lock and commit without replacing native database execution.</summary>
internal sealed class GrantLifecycleRaceProbe(bool winner) : DbCommandInterceptor, IDbTransactionInterceptor
{
    private const string Governance = "AdministratorGovernance";
    private const string Update = "UPDATE";
    private PlatformDbContext? owner;
    private Guid? transactionId;
    private int commits;
    internal bool Armed
    {
        get; set;
    }
    internal TaskCompletionSource<int> Lock { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource<int> Committing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Binds the owned context and exact provider session immediately before its real governance UPDATE.</summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Armed && command.CommandText.TrimStart().StartsWith(Update, StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains(Governance, StringComparison.Ordinal))
        {
            var database = Assert.IsType<PlatformDbContext>(data.Context);
            Assert.NotNull(database.Database.CurrentTransaction);
            if (owner is not null)
            {
                Assert.Same(owner, database);
            }
            owner = database;
            Lock.TrySetResult(NativeLockObservation.Session(command.Connection!));
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Holds only the selected winner after all production checks and writes, immediately before native commit.</summary>
    public async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData data,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (owner is not null && ReferenceEquals(owner, data.Context))
        {
            Assert.Null(transactionId);
            transactionId = data.TransactionId;
            if (winner)
            {
                Committing.TrySetResult(NativeLockObservation.Session(transaction.Connection!));
                await Release.Task.WaitAsync(cancellationToken);
            }
        }
        return result;
    }

    /// <summary>Counts only completed provider commits belonging to the observed operation.</summary>
    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData data, CancellationToken cancellationToken = default)
    {
        if (owner is not null && ReferenceEquals(owner, data.Context))
        {
            Assert.Equal(transactionId, data.TransactionId);
            Interlocked.Increment(ref commits);
        }
        return Task.CompletedTask;
    }

    /// <summary>A denied loser must never reach commit, while the winner must have one actual native commit.</summary>
    internal void RequireOutcome()
    {
        Assert.NotNull(owner);
        Assert.Equal(winner ? 1 : 0, commits);
        Assert.Equal(winner, transactionId is not null);
    }
}
