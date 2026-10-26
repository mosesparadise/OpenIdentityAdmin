using System.Data.Common;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Loses acknowledgement only for the exact native PAR owner observed after token persistence.</summary>
internal sealed class BrowserAuditCommitLoss : DbTransactionInterceptor
{
    private const string Message = "Synthetic browser audit commit acknowledgement loss.";
    private PlatformDbContext? owner;
    internal InvalidOperationException Failure { get; } = new(Message);
    internal int Commits
    {
        get; private set;
    }

    /// <summary>Targets the actual native attachment transaction, leaving earlier durable assertion admission intact.</summary>
    internal void Arm(PlatformDbContext database) => owner = database;

    /// <summary>Throws only after the targeted real provider commit; the original exception is retained as the failure witness.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (owner is not null && ReferenceEquals(owner, eventData.Context))
        {
            Commits++;
            throw Failure;
        }
        return Task.CompletedTask;
    }
}
