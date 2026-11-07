using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.AuthServer.Features.Protocol.PilotReplay;

/// <summary>Retains the strict pilot transaction fence through native commit entry.</summary>
internal sealed class PilotCommitFence : DbTransactionInterceptor
{
    /// <summary>Rejects a synchronous commit outside its exact live transaction.</summary>
    public override InterceptionResult TransactionCommitting(DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
    {
        PilotStorageFence.Current?.RequireCommit(eventData.Context, transaction);
        return result;
    }
    /// <summary>Rejects an asynchronous commit outside its exact live transaction.</summary>
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        PilotStorageFence.Current?.RequireCommit(eventData.Context, transaction);
        return ValueTask.FromResult(result);
    }
}
