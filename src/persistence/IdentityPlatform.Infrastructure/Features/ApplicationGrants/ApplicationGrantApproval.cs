using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Retains immutable approved intent and safe admission provenance, independently of the approving browser session.</summary>
internal sealed class ApplicationGrantApproval
{
    public Guid Id
    {
        get; set;
    }
    public Guid JobId
    {
        get; set;
    }
    public string ActorId { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public GrantApprovalAction Action
    {
        get; set;
    }
    public ApplicationLifecycleReason Reason
    {
        get; set;
    }
    public Guid RequestKey
    {
        get; set;
    }
    public string ScopedKeyHash { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public long ApplicationRevision
    {
        get; set;
    }
    public long ApprovedAt
    {
        get; set;
    }
    public long FactorVerifiedAt
    {
        get; set;
    }
    public long ReplayUntil
    {
        get; set;
    }
    public Guid AuditId
    {
        get; set;
    }
}

/// <summary>Separates first acceptance from explicit continuation in idempotency and immutable history.</summary>
internal enum GrantApprovalAction
{
    Submit = 1, Continue = 2
}
