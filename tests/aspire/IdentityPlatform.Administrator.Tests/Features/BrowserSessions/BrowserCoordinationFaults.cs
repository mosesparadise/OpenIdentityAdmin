using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Runs a test observation after the actual final save, preserving all provider writes and transaction behavior.</summary>
internal sealed class BrowserSaveObservation(Action observe) : SaveChangesInterceptor
{
    internal int Calls
    {
        get; private set;
    }
    /// <summary>Allows expiry or caller mutation only after native persistence has completed inside the still-owned transaction.</summary>
    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Calls++;
        if (Calls == 1)
        {
            observe();
        }
        return ValueTask.FromResult(result);
    }
}

/// <summary>Models a lost acknowledgement only after a genuine provider commit, with an exact attempt witness.</summary>
internal sealed class BrowserCommitLoss : DbTransactionInterceptor
{
    private const string LostAcknowledgement = "Synthetic browser commit acknowledgement loss.";

    internal InvalidOperationException Failure { get; } = new(LostAcknowledgement);
    internal int Commits
    {
        get; private set;
    }

    /// <summary>Fails after the native transaction succeeds so a fresh reader can distinguish caller uncertainty from rollback.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Commits++;
        throw Failure;
    }
}
