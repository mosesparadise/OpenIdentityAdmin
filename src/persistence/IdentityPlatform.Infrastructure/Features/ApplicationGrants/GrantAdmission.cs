using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
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
        target.RequireJob(intent.JobId);
        var replay = await ReplayAsync(scope, intent, actor, token);
        if (replay is not null)
        {
            target.RequireJob(replay.Id);
            await target.VerifyAsync(scope, token);
            return replay;
        }
        GrantTarget.RequireDisabled(policy, intent.Version);
        var now = scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds();
        if (now < policy.ObservedAt || factorVerifiedAt > now || factorVerifiedAt < 0)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var job = await JobAsync(scope, intent, policy, now, token);
        var content = new GrantApprovalContent(actor, intent.ApplicationId, intent.Action, intent.Reason,
            intent.RequestKey, intent.ScopeHash(actor), intent.RequestHash());
        var accepted = await SaveApprovalAsync(scope, job, content, policy.Revision, factorVerifiedAt, now, correlation, token);
        await target.VerifyAsync(scope, token);
        return accepted.Job;
    }

    /// <summary>Creates a new Submit approval in the retirement namespace; it never adopts, continues or replays an ordinary grant job.</summary>
    internal static async Task<GrantAdmissionState> StageRetirementAsync(ProtocolStorageScope scope, ApplicationRetirementIntent intent,
        ApplicationPolicy policy, string actor, long factorVerifiedAt, long now, Guid correlation, CancellationToken token)
    {
        if (!policy.Retired || policy.Enabled || policy.ApplicationId != intent.ApplicationId || now != policy.ObservedAt ||
            factorVerifiedAt < 0 || factorVerifiedAt > now)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var job = await NewJobAsync(scope, policy, now, token);
        var content = new GrantApprovalContent(actor, intent.ApplicationId, GrantApprovalAction.Submit, intent.Reason,
            intent.RequestKey, intent.ScopeHash(actor), intent.RequestHash());
        return await SaveApprovalAsync(scope, job, content, policy.Revision, factorVerifiedAt, now, correlation, token);
    }

    /// <summary>Shares only immutable first/continued approval persistence, returning a verifier for any later saves by the owning admission.</summary>
    private static async Task<GrantAdmissionState> SaveApprovalAsync(ProtocolStorageScope scope, ApplicationGrantJob job, GrantApprovalContent content,
        long revision, long factorVerifiedAt, long now, Guid correlation, CancellationToken token)
    {
        var audit = GrantAudit.Intent(job, content.ActorId, correlation, now,
            content.Action == GrantApprovalAction.Submit ? GrantAudit.Accepted : GrantAudit.Continued);
        var approval = Approval(content, job.Id, revision, factorVerifiedAt, now, audit.Id);
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
        return new(job, approval.Id, expected);
    }

    /// <summary>Captures immutable approval provenance separately from the job's evolving progress and originating replay namespace.</summary>
    private static ApplicationGrantApproval Approval(GrantApprovalContent content, Guid jobId, long revision, long factorVerifiedAt, long now, Guid auditId) => new()
    {
        Id = Guid.NewGuid(),
        JobId = jobId,
        ActorId = content.ActorId,
        ApplicationId = content.ApplicationId,
        Action = content.Action,
        Reason = content.Reason,
        RequestKey = content.RequestKey,
        ScopedKeyHash = content.ScopedKeyHash,
        RequestHash = content.RequestHash,
        ApplicationRevision = revision,
        ApprovedAt = now,
        FactorVerifiedAt = factorVerifiedAt,
        ReplayUntil = checked(now + ReplaySeconds),
        AuditId = auditId
    };

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
                row.RequestHash != intent.RequestHash() || row.ScopedKeyHash != digest || now < row.ApprovedAt || now >= row.ReplayUntil ||
                intent.JobId is { } requestedJob && row.JobId != requestedJob)
            {
                throw new ApplicationMutationRejected(ApplicationMutationFailure.Duplicate);
            }
            var job = await scope.Owner.Database.Set<ApplicationGrantJob>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == row.JobId, current);
            return job is not null && job.ApplicationId == intent.ApplicationId ? job : throw ProtocolStorageOwner.Unavailable();
        }, token);

    /// <summary>Separates exact stopped-job continuation from new admission without allowing either path to adopt unrelated work.</summary>
    private static ValueTask<ApplicationGrantJob> JobAsync(ProtocolStorageScope scope, GrantIntent intent, ApplicationPolicy policy, long now, CancellationToken token) =>
        intent.JobId is { } id ? ContinueAsync(scope, intent, id, now, token) : NewJobAsync(scope, policy, now, token);

    /// <summary>Resumes only the exact observed stopped job while preserving its original application and admission revision.</summary>
    private static ValueTask<ApplicationGrantJob> ContinueAsync(ProtocolStorageScope scope, GrantIntent intent, Guid id, long now, CancellationToken token) =>
        scope.Owner.RunAsync(async current =>
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
        }, token, write: true);

    /// <summary>Rejects every incomplete predecessor, including stopped work, and stages a distinct newly approved job.</summary>
    private static ValueTask<ApplicationGrantJob> NewJobAsync(ProtocolStorageScope scope, ApplicationPolicy policy, long now, CancellationToken token) =>
        scope.Owner.RunAsync(async current =>
        {
            if (await scope.Owner.Database.Set<ApplicationGrantJob>().AnyAsync(value => value.ApplicationId == policy.ApplicationId && value.State != GrantJobState.Succeeded, current))
            {
                throw new ApplicationMutationRejected(ApplicationMutationFailure.Duplicate);
            }
            var job = new ApplicationGrantJob
            {
                Id = Guid.NewGuid(),
                ApplicationId = policy.ApplicationId,
                ApplicationRevision = policy.Revision,
                CreatedAt = now,
                ObservedAt = now
            };
            scope.Owner.Database.Add(job);
            return job;
        }, token, write: true);
}
