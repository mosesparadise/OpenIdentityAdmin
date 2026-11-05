using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Stages durable acceptance or explicit recovery inside the API's already authorized owned transaction.</summary>
internal static class GrantAdmission
{
    private const string GuidFormat = "D";

    private const long ReplaySeconds = 24 * 60 * 60;

    /// <summary>Returns an exact current authorized replay or stages a new immutable approval; caller retains commit/publication ownership.</summary>
    internal static async Task<ApplicationGrantJob> StageAsync(ProtocolStorageScope scope, GrantIntent intent, string actor,
        long factorVerifiedAt, Guid correlation, CancellationToken token)
    {
        var target = await GrantTarget.ReadAsync(scope, intent.ApplicationId, token);
        var policy = target.Policy;
        var replay = await ReplayAsync(scope, intent, actor, token);
        if (replay is not null)
        {
            return replay;
        }
        GrantTarget.RequireDisabled(policy, intent.Version);
        var now = scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds();
        if (now < policy.ObservedAt || factorVerifiedAt > now || factorVerifiedAt < 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var job = await JobAsync(scope, intent, policy, now, token);
        var audit = GrantAudit.Intent(job, actor, correlation, now, intent.Action == GrantApprovalAction.Submit ? GrantAudit.Accepted : GrantAudit.Continued);
        var approval = Approval(intent, job.Id, actor, policy.Revision, factorVerifiedAt, now, audit.Id);
        job.EffectiveApprovalId = approval.Id;
        var detail = GrantAudit.Detail(job, audit.Id, 0, 0);
        var expected = new GrantPersistence(job, approval, audit, detail);
        await scope.Owner.RunAsync(async current =>
        {
            scope.Owner.Database.Add(audit);
            scope.Owner.Database.Add(approval);
            scope.Owner.Database.Add(detail);
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);
        await expected.VerifyAsync(scope, token);
        await target.VerifyAsync(scope, token);
        return job;
    }

    /// <summary>Captures immutable approval provenance separately from the job's evolving progress.</summary>
    private static ApplicationGrantApproval Approval(GrantIntent intent, Guid jobId, string actor, long revision, long factorVerifiedAt, long now, Guid auditId)
    {
        return new ApplicationGrantApproval
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            ActorId = actor,
            ApplicationId = intent.ApplicationId,
            Action = intent.Action,
            Reason = intent.Reason,
            RequestKey = intent.RequestKey,
            ScopedKeyHash = intent.ScopeHash(actor),
            RequestHash = intent.RequestHash(),
            ApplicationRevision = revision,
            ApprovedAt = now,
            FactorVerifiedAt = factorVerifiedAt,
            ReplayUntil = checked(now + ReplaySeconds),
            AuditId = auditId
        };
    }

    /// <summary>Scopes replay by exact identity and intent, with an explicit retained-key expiry that never expires worker authority.</summary>
    private static ValueTask<ApplicationGrantJob?> ReplayAsync(ProtocolStorageScope scope, GrantIntent intent, string actor, CancellationToken token) =>
        scope.Owner.RunAsync(async current =>
        {
            var digest = intent.ScopeHash(actor);
            var row = await scope.Owner.Database.Set<ApplicationGrantApproval>().AsNoTracking().SingleOrDefaultAsync(value => value.ScopedKeyHash == digest, current);
            if (row is null)
            {
                return null;
            }
            var now = scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds();
            if (row.ActorId != actor || row.Action != intent.Action || row.ApplicationId != intent.ApplicationId || row.RequestKey != intent.RequestKey ||
                row.RequestHash != intent.RequestHash() || row.ScopedKeyHash != digest || now < row.ApprovedAt || now >= row.ReplayUntil)
            {
                throw new ApplicationMutationRejected(ApplicationMutationFailure.Duplicate);
            }
            var job = await scope.Owner.Database.Set<ApplicationGrantJob>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == row.JobId, current);
            return job is not null && job.ApplicationId == intent.ApplicationId ? job : throw ProtocolStorageOwner.Unavailable();
        }, token);

    /// <summary>Serializes one incomplete job per application and only resumes stopped work under its exact observed version.</summary>
    private static ValueTask<ApplicationGrantJob> JobAsync(ProtocolStorageScope scope, GrantIntent intent, ApplicationPolicy policy, long now, CancellationToken token) =>
        scope.Owner.RunAsync(async current =>
        {
            if (intent.JobId is { } id)
            {
                var existing = await scope.Owner.Database.Set<ApplicationGrantJob>().SingleOrDefaultAsync(value => value.Id == id, current);
                if (existing is null || existing.ApplicationId != intent.ApplicationId)
                {
                    throw new ApplicationMutationRejected(ApplicationMutationFailure.Missing);
                }
                if (!ApplicationVersion.Matches(intent.JobVersion, id.ToString(GuidFormat), existing.Revision))
                {
                    throw new ApplicationMutationRejected(ApplicationMutationFailure.Stale);
                }
                if (existing.State != GrantJobState.Stopped)
                {
                    throw new ApplicationMutationRejected(ApplicationMutationFailure.SameState);
                }
                if (now < existing.ObservedAt)
                {
                    throw ProtocolStorageOwner.Unavailable();
                }
                existing.Revision = checked(existing.Revision + 1);
                existing.State = GrantJobState.Queued;
                existing.Failure = GrantJobFailure.None;
                existing.ObservedAt = now;
                return existing;
            }
            if (await scope.Owner.Database.Set<ApplicationGrantJob>().AnyAsync(value => value.ApplicationId == intent.ApplicationId && value.State != GrantJobState.Succeeded, current))
            {
                throw new ApplicationMutationRejected(ApplicationMutationFailure.Duplicate);
            }
            var job = new ApplicationGrantJob
            {
                Id = Guid.NewGuid(),
                ApplicationId = intent.ApplicationId,
                ApplicationRevision = policy.Revision,
                CreatedAt = now,
                ObservedAt = now
            };
            scope.Owner.Database.Add(job);
            return job;
        }, token, write: true);
}
