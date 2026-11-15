using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Stages irreversible terminal policy and newly approved durable cleanup in the caller's one authorized transaction.</summary>
internal static class ApplicationRetirementAdmission
{
    /// <summary>Admits one exact retirement or its original authorized replay; no ordinary mutation or grant operation is recursively invoked.</summary>
    internal static async Task<ApplicationRetirementState> StageAsync(ProtocolStorageScope scope, ApplicationRetirementIntent intent,
        string actor, long factorVerifiedAt, Guid correlation, CancellationToken token)
    {
        var target = await GrantTarget.ReadAsync(scope, intent.ApplicationId, token);
        if (target.Retirement is not null)
        {
            return await ReplayAsync(scope, target, intent, actor, token);
        }
        var policy = target.Policy;
        if (!ApplicationVersion.Matches(intent.Version, intent.ApplicationId, policy.Revision))
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Stale);
        }
        var now = scope.Owner.Clock.GetUtcNow().ToUnixTimeSeconds();
        var application = await scope.Owner.RunAsync(current =>
        {
            scope.Owner.Database.Attach(policy);
            return new ValueTask<ApplicationWriteState>(target.Retire(now));
        }, token, write: true);
        var accepted = await GrantAdmission.StageRetirementAsync(scope, intent, policy, actor, factorVerifiedAt, now, correlation, token);
        var audit = GrantAudit.Intent(accepted.Job, actor, correlation, now, ApplicationRetirementBinding.Event);
        var receipt = Create(intent, policy, actor, accepted, now, audit.Id);
        var expected = new ApplicationRetirementPersistence(receipt, audit);
        await scope.Owner.RunAsync(async current =>
        {
            scope.Owner.Database.AddRange(receipt, audit);
            await scope.Owner.Database.SaveChangesAsync(current);
        }, token, write: true);
        var result = new ApplicationRetirementState(receipt, accepted.Job, application, accepted.Persistence, expected);
        await result.VerifyAsync(scope, token);
        return result;
    }

    /// <summary>Checks original actor, key, version, reason and namespace without renewing authority, adopting work or freezing past job progress.</summary>
    private static async Task<ApplicationRetirementState> ReplayAsync(ProtocolStorageScope scope, GrantTargetObservation target,
        ApplicationRetirementIntent intent, string actor, CancellationToken token)
    {
        var binding = target.Retirement!;
        var receipt = binding.Receipt;
        if (receipt.ActorId != actor || receipt.RequestKey != intent.RequestKey || receipt.Reason != intent.Reason ||
            !ApplicationVersion.Matches(intent.Version, receipt.ApplicationId, receipt.OriginalRevision) ||
            receipt.ScopedKeyHash != intent.ScopeHash(actor) || receipt.RequestHash != intent.RequestHash())
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Duplicate);
        }
        var result = new ApplicationRetirementState(target, await binding.JobAsync(scope, token));
        result.RequireCurrent(scope.Owner.Clock);
        return result;
    }

    /// <summary>Stores original and terminal revisions and explicit original approval linkage independently of future job continuation.</summary>
    private static ApplicationRetirement Create(ApplicationRetirementIntent intent, ApplicationPolicy policy, string actor,
        GrantAdmissionState accepted, long now, Guid auditId) => new()
        {
            ApplicationId = intent.ApplicationId,
            ActorId = actor,
            OriginalRevision = checked(policy.Revision - 1),
            RetiredRevision = policy.Revision,
            Reason = intent.Reason,
            RetiredAt = now,
            RequestKey = intent.RequestKey,
            ScopedKeyHash = intent.ScopeHash(actor),
            RequestHash = intent.RequestHash(),
            ReplayUntil = checked(now + ApplicationRetirementIntent.ReplaySeconds),
            AuditId = auditId,
            GrantJobId = accepted.Job.Id,
            GrantApprovalId = accepted.ApprovalId
        };
}
