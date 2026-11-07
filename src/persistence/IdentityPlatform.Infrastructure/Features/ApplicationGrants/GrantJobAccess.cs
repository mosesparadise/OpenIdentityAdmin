using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Reads exact job/approval associations; candidate reads never themselves authorize a batch.</summary>
internal static class GrantJobAccess
{
    /// <summary>Uses detached state before ordered account locking and then lets callers compare the authoritative reread.</summary>
    internal static ValueTask<GrantJobPair?> ReadAsync(ProtocolStorageScope scope, Guid id, CancellationToken token, bool tracking = false) =>
        scope.Owner.RunAsync(async current =>
        {
            var jobs = scope.Owner.Database.Set<ApplicationGrantJob>().AsQueryable();
            var job = await (tracking ? jobs : jobs.AsNoTracking()).SingleOrDefaultAsync(row => row.Id == id, current);
            if (job is null)
            {
                return null;
            }
            var approval = await scope.Owner.Database.Set<ApplicationGrantApproval>().AsNoTracking()
                .SingleOrDefaultAsync(row => row.Id == job.EffectiveApprovalId, current);
            if (approval is null || approval.JobId != job.Id || approval.ApplicationId != job.ApplicationId ||
                string.IsNullOrWhiteSpace(approval.ActorId) || approval.ActorId.Length > FactorSchema.UserLength ||
                approval.ApplicationRevision <= 0 || approval.ApprovedAt < 0 || approval.FactorVerifiedAt < 0 ||
                approval.FactorVerifiedAt > approval.ApprovedAt || approval.ReplayUntil <= approval.ApprovedAt ||
                job.ObservedAt < approval.ApprovedAt || job.Revision <= 0 || job.AuthorizationCount < 0 || job.TokenCount < 0 ||
                !Enum.IsDefined(job.State) || !Enum.IsDefined(job.Failure) ||
                (job.State == GrantJobState.Stopped) != (job.Failure != GrantJobFailure.None))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            return new GrantJobPair(job, approval);
        }, token);
}

/// <summary>Pairs a current job with the effective immutable approval from the same owned observation.</summary>
internal sealed record GrantJobPair(ApplicationGrantJob Job, ApplicationGrantApproval Approval)
{
    internal bool Runnable => Job.State is GrantJobState.Queued or GrantJobState.Processing;
}
