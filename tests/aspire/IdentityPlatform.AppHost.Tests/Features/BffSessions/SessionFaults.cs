using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.AppHost.Tests.Features.BffSessions;

/// <summary>Injects failures only at native audit commands after the state write has executed in the same transaction.</summary>
internal sealed class AuditFault(Action? beforeAudit = null, bool fail = false) : DbCommandInterceptor
{
    private const string Insert = "INSERT";
    private const string AuditTable = "AdminSessionAudit";
    private const string Failure = "Synthetic audit failure.";
    internal int Attempts
    {
        get; private set;
    }

    /// <summary>Observes native INSERT/OUTPUT or INSERT/RETURNING audit execution without inspecting parameter contents.</summary>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains(Insert, StringComparison.Ordinal) && command.CommandText.Contains(AuditTable, StringComparison.Ordinal))
        {
            Attempts++;
            beforeAudit?.Invoke();
            if (fail)
            {
                throw new InvalidOperationException(Failure);
            }
        }
        return ValueTask.FromResult(result);
    }
}

/// <summary>Simulates a lost acknowledgement after the native transaction committed, without rerunning the operation.</summary>
internal sealed class CommitResponseFault : DbTransactionInterceptor
{
    private const string Failure = "Synthetic lost commit acknowledgement.";
    internal int Commits
    {
        get; private set;
    }

    /// <summary>Fails after provider commit so persisted state and the caller's uncertainty can be independently asserted.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Commits++;
        throw new InvalidOperationException(Failure);
    }
}

/// <summary>Synchronizes competing native UPDATE statements after both stores have authenticated the same original revision.</summary>
internal sealed class TicketWriteBarrier(int participants, bool deletion = false) : DbCommandInterceptor
{
    private const string Update = "UPDATE";
    private const string Delete = "DELETE";
    private const string TicketTable = "AdminSessionTicket";
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int arrivals;
    internal Task Entered => entered.Task;

    /// <summary>Holds only the first requested number of ticket updates; cancellation releases no successful operation.</summary>
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains(deletion ? Delete : Update, StringComparison.Ordinal) && command.CommandText.Contains(TicketTable, StringComparison.Ordinal) &&
            Interlocked.Increment(ref arrivals) <= participants)
        {
            if (arrivals == participants)
            {
                entered.TrySetResult();
            }
            await released.Task.WaitAsync(cancellationToken);
        }
        return result;
    }

    /// <summary>Releases the held native statements only after the test has established the intended interleaving.</summary>
    internal void Release() => released.TrySetResult();
}
