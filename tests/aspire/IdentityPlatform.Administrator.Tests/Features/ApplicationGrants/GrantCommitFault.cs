using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Fails once immediately before or after an actual provider commit, independently of native save interceptors.</summary>
internal sealed class GrantCommitFault(bool after) : DbTransactionInterceptor
{
    internal bool Armed
    {
        get; set;
    }
    internal bool Observed
    {
        get; private set;
    }

    /// <summary>Throws before the real commit only in the rollback arm; subsequent reconciliation uses an unarmed interceptor.</summary>
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        Fail(afterCommit: false);
        return ValueTask.FromResult(result);
    }

    /// <summary>Loses only the acknowledgement after the relational provider committed, never simulating a commit with a return flag.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Fail(afterCommit: true);
        return Task.CompletedTask;
    }

    /// <summary>Records actual interception and consumes the fault before throwing so fresh recovery can observe storage truth.</summary>
    private void Fail(bool afterCommit)
    {
        if (!Armed || afterCommit != after)
        {
            return;
        }
        Armed = false;
        Observed = true;
        throw new IOException();
    }
}
