using IdentityPlatform.Core.Features.ApplicationGrants;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Creates synthetic relational witnesses only, without claiming genuine administrator approval.</summary>
internal static class GrantSchemaRows
{
    private const long Created = 1000;
    private const long ReplayUntil = 87400;
    private const int ApplicationLength = 400;
    private const int ActorLength = 450;
    private const int HashLength = 64;
    private const string Event = "SyntheticGrantSchemaWitness";
    private const string User = "User";
    private const string Application = "Application";
    private const string Accepted = "Accepted";

    /// <summary>Uses maximum supported identifiers so either database rejects accidental field or index narrowing.</summary>
    internal static (ApplicationPolicy Policy, ApplicationGrantJob Job, ApplicationGrantApproval Approval, ApplicationGrantAudit Detail) Add(PlatformDbContext database)
    {
        var policy = ManagedSchemaRows.Policy();
        policy.ApplicationId = new string('a', ApplicationLength);
        policy.Enabled = false;
        var audit = new SecurityAuditIntent { Event = Event, ActorType = User, TargetType = Application, Outcome = Accepted, OccurredAt = Created };
        var job = new ApplicationGrantJob
        {
            Id = Guid.NewGuid(),
            ApplicationId = policy.ApplicationId,
            ApplicationRevision = policy.Revision,
            CreatedAt = Created,
            ObservedAt = Created,
            EffectiveApprovalId = Guid.NewGuid()
        };
        var approval = new ApplicationGrantApproval
        {
            Id = job.EffectiveApprovalId,
            JobId = job.Id,
            ActorId = new string('u', ActorLength),
            ApplicationId = policy.ApplicationId,
            Action = GrantApprovalAction.Submit,
            Reason = ApplicationLifecycleReason.Security,
            RequestKey = Guid.NewGuid(),
            ScopedKeyHash = new string('a', HashLength),
            RequestHash = new string('b', HashLength),
            ApplicationRevision = policy.Revision,
            ApprovedAt = Created,
            FactorVerifiedAt = Created,
            ReplayUntil = ReplayUntil,
            AuditId = audit.Id
        };
        var detail = new ApplicationGrantAudit { AuditId = audit.Id, JobId = job.Id, ApprovalId = approval.Id, JobRevision = 1, State = GrantJobState.Queued };
        database.AddRange(policy, audit, job, approval, detail);
        return (policy, job, approval, detail);
    }
}
