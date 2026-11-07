using System.Data.Common;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Observes actual gate UPDATE sessions; absent instrumentation never counts as database contention.</summary>
internal sealed class PilotReplayGateProbe : DbCommandInterceptor
{
    private const string MissingOwner = "The contender has no distinct observed gate owner.";

    private const string Gate = "PilotProtocolGate";
    private const string Update = "UPDATE";
    private const int ObserveSeconds = 2;
    private readonly TaskCompletionSource<int> contender = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object sync = new();
    private DbTransaction? transaction;
    private int completedSession;
    private int holder;
    private bool observing;

    /// <summary>Attaches only a passive command observer to the production native database pipeline.</summary>
    internal void Configure(IServiceCollection services) => services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this, new GateTransactions(this)));

    /// <summary>Freezes only a still-attached gate transaction before the competing request starts; completed validation is not a generation owner.</summary>
    internal void Begin()
    {
        lock (sync)
        {
            holder = transaction is not null ? completedSession : 0;
            observing = true;
        }
    }

    /// <summary>Captures the exact contender connection before its native gate command executes.</summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        lock (sync)
        {
            if (observing && holder > 0 && Matches(command))
            {
                contender.TrySetResult(NativeLockObservation.Session(command.Connection!));
            }
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Records only successful, transaction-bound acquisitions with exactly one matched singleton.</summary>
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        lock (sync)
        {
            if (!observing && result == 1 && Matches(command))
            {
                transaction = command.Transaction;
                completedSession = NativeLockObservation.Session(command.Connection!);
            }
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Stops only between native queries; timeout and query failures remain distinct from a completed response-first observation.</summary>
    internal async Task<bool> ObserveAsync(string provider, string connection, Task stop, CancellationToken cancellationToken)
    {
        var first = await Task.WhenAny(contender.Task, stop).WaitAsync(TimeSpan.FromSeconds(ObserveSeconds), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (first == stop)
        {
            return false;
        }
        var peer = await contender.Task;
        Assert.True(holder > 0 && peer != holder, MissingOwner);
        return await new NativeLockObservation(provider, connection).ObserveUntilAsync(peer, holder, stop, cancellationToken);
    }

    /// <summary>Clears only the exact completed acquisition, without dereferencing a disposed child context.</summary>
    private void Completed(DbTransaction completed)
    {
        lock (sync)
        {
            if (ReferenceEquals(transaction, completed))
            {
                transaction = null;
            }
        }
    }

    /// <summary>Observes actual transaction completion; dispatching commit alone never claims that ownership ended.</summary>
    private sealed class GateTransactions(PilotReplayGateProbe owner) : DbTransactionInterceptor
    {
        /// <summary>Forgets a successfully committed gate before its child scope is disposed.</summary>
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            owner.Completed(transaction);
            return Task.CompletedTask;
        }

        /// <summary>Forgets a rolled-back gate before its child scope is disposed.</summary>
        public override Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            owner.Completed(transaction);
            return Task.CompletedTask;
        }

        /// <summary>A failed transaction is not evidence of a retained winning gate.</summary>
        public override Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            owner.Completed(transaction);
            return Task.CompletedTask;
        }
    }

    /// <summary>Limits instrumentation to the reviewed coordination statement, excluding arbitrary native writes.</summary>
    private static bool Matches(DbCommand command) => command.Transaction is not null &&
        command.CommandText.TrimStart().StartsWith(Update, StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains(Gate, StringComparison.Ordinal);
}
