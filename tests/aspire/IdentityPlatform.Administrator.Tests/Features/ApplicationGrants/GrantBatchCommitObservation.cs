using System.Data.Common;
using System.Diagnostics;
using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Features.ApplicationGrants;

/// <summary>Starts the revocation visibility clock at the actual provider callback for one selected progress commit.</summary>
internal sealed class GrantBatchCommitObservation : DbTransactionInterceptor
{
    private const int VisibilitySeconds = 5;
    internal Guid Job
    {
        get; set;
    }
    private long? committed;

    /// <summary>Ignores setup/admission and records exactly one native progress transaction without pausing or replacing commit.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData data, CancellationToken cancellationToken = default)
    {
        if (Job != Guid.Empty && data.Context!.ChangeTracker.Entries<ApplicationGrantJob>()
            .Any(row => row.Entity.Id == Job && row.Entity.State == GrantJobState.Processing))
        {
            Assert.Null(committed);
            committed = Stopwatch.GetTimestamp();
        }
        return Task.CompletedTask;
    }

    /// <summary>Requires completed entry validation inside five real elapsed seconds from durable commit, independently of the controlled product clock.</summary>
    internal void RequireVisible()
    {
        Assert.NotNull(committed);
        Assert.True(Stopwatch.GetElapsedTime(committed.Value) <= TimeSpan.FromSeconds(VisibilitySeconds));
    }
}
