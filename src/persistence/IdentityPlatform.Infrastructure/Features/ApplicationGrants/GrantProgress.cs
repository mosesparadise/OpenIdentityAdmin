using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Stages and verifies one exact progress/audit revision without independently committing or authorizing native work.</summary>
internal static class GrantProgress
{
    /// <summary>Advances only committed-intent counters; the caller must still verify native rows/current authority and commit the owning transaction.</summary>
    internal static async Task<GrantPersistence> StageAsync(ProtocolStorageScope scope, GrantJobPair pair, GrantBatchCount count,
        GrantJobFailure failure, Guid auditId, Guid correlation, CancellationToken token)
    {
        var job = pair.Job;
        var now = scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds();
        if (now < job.ObservedAt && failure != GrantJobFailure.Clock)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        job.Revision = checked(job.Revision + 1);
        job.AuthorizationCount = checked(job.AuthorizationCount + count.Authorizations);
        job.TokenCount = checked(job.TokenCount + count.Tokens);
        job.ObservedAt = Math.Max(now, job.ObservedAt);
        job.Failure = failure;
        job.State = failure != GrantJobFailure.None ? GrantJobState.Stopped :
            count.Authorizations == 0 && count.Tokens == 0 ? GrantJobState.Succeeded : GrantJobState.Processing;
        var action = failure != GrantJobFailure.None ? GrantAudit.Stopped : job.State == GrantJobState.Succeeded ? GrantAudit.Succeeded : GrantAudit.Advanced;
        var audit = GrantAudit.Intent(job, pair.Approval.ActorId, correlation, job.ObservedAt, action);
        audit.Id = auditId;
        var detail = GrantAudit.Detail(job, auditId, count.Authorizations, count.Tokens);
        var expected = new GrantPersistence(job, pair.Approval, audit, detail);
        await scope.Owner.RunAsync(async current =>
        {
            if (scope.Owner.Database.Entry(job).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            {
                scope.Owner.Database.Attach(job);
                scope.Owner.Database.Entry(job).Property(value => value.Revision).OriginalValue = job.Revision - 1;
                scope.Owner.Database.Entry(job).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            }
            scope.Owner.Database.Add(audit);
            scope.Owner.Database.Add(detail);
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);
        await expected.VerifyAsync(scope, token);
        return expected;
    }
}
