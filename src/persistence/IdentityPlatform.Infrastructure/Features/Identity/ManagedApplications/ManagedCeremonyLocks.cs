using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Proves the full lock order was acquired in the exact live owned phase; a caller-supplied plan alone grants no credential capability.</summary>
internal sealed class ManagedCeremonyLocks
{
    private readonly ProtocolStorageOwner owner;
    private readonly ProtocolStoragePhase phase;
    private readonly IDbContextTransaction transaction;
    internal CeremonyLockPlan Plan
    {
        get;
    }

    /// <summary>Captures only the successfully acquired exact context and database transaction.</summary>
    private ManagedCeremonyLocks(ProtocolStorageOwner owner, CeremonyLockPlan plan)
    {
        this.owner = owner;
        Plan = plan;
        phase = owner.Capture(write: true);
        transaction = owner.Database.Database.CurrentTransaction!;
    }

    /// <summary>Acquires governance, admission, existing browser budgets and accounts in the shared portable order.</summary>
    internal static ValueTask<ManagedCeremonyLocks> AcquireAsync(ProtocolStorageScope scope, CeremonyLockPlan plan, CancellationToken token) =>
        scope.Owner.RunAsync(async current =>
        {
            if (!plan.Governance || !plan.Admission)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            await new CeremonyLocks((PlatformDbContext)scope.Owner.Database).AcquireAsync(plan, current);
            return new ManagedCeremonyLocks(scope.Owner, plan);
        }, token, write: true);

    /// <summary>Rejects escaped execution, wrong phase/context/transaction and completed ownership before credential work.</summary>
    internal void Require(PlatformDbContext database)
    {
        owner.RequireContext(database);
        if (!ReferenceEquals(phase, owner.Capture(write: true)) || !ReferenceEquals(transaction, database.Database.CurrentTransaction))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}
