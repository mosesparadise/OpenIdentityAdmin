using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Reconciles uncertain commits and records a stopped outcome under fresh governance ownership without revoking further entries.</summary>
internal static class GrantFailure
{
    /// <summary>Never reports a durable stop when storage or cleanup prevented it; the original failed scope is not reused.</summary>
    internal static async Task<bool> RecordAsync(IServiceScopeFactory factory, GrantAttempt attempt, TimeProvider clock, CancellationToken token)
    {
        if (attempt.ApprovalId is null)
        {
            return false;
        }
        var request = new ProtocolStorageRequest(clock, token);
        try
        {
            return await request.RunAsync(factory, (scope, current) => OwnedAsync(scope, attempt, current));
        }
        finally { await request.DrainAsync(); }
    }

    /// <summary>Stops incomplete work under the same approval even after another batch advanced, while preserving newer approvals and success.</summary>
    private static async ValueTask<bool> OwnedAsync(ProtocolStorageScope scope, GrantAttempt attempt, CancellationToken token)
    {
        scope.Services.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.Owned);
        await using var transaction = await ProtocolStorageTransaction.OpenAsync(scope.Owner);
        await scope.Owner.RunAsync(current => new ValueTask(new CeremonyLocks((PlatformDbContext)scope.Owner.Database)
            .AcquireAsync(new CeremonyLockPlan(true, false, [], []), current)), token, write: true);
        var pair = await GrantJobAccess.ReadAsync(scope, attempt.JobId, token, tracking: true);
        if (pair is null || pair.Approval.Id != attempt.ApprovalId)
        {
            return false;
        }
        var committed = await scope.Owner.RunAsync(async current => await scope.Owner.Database.Set<ApplicationGrantAudit>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.AuditId == attempt.AuditId, current), token);
        if (committed is not null && attempt.CommitAttempted)
        {
            if (committed.JobId != pair.Job.Id || committed.ApprovalId != pair.Approval.Id || committed.JobRevision > pair.Job.Revision)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            return true;
        }
        if (!pair.Runnable)
        {
            return false;
        }
        var failure = scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds() < pair.Job.ObservedAt ? GrantJobFailure.Clock : attempt.Failure;
        await GrantProgress.StageAsync(scope, pair, new([], []), failure, Guid.NewGuid(), attempt.Correlation, token);
        await transaction.CommitAsync(token);
        return true;
    }
}
