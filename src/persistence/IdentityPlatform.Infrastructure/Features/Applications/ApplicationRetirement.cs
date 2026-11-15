using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Retains one immutable terminal admission and its original approval independently of evolving grant-job progress.</summary>
internal sealed class ApplicationRetirement
{
    public string ApplicationId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public long OriginalRevision
    {
        get; set;
    }
    public long RetiredRevision
    {
        get; set;
    }
    public ApplicationLifecycleReason Reason
    {
        get; set;
    }
    public long RetiredAt
    {
        get; set;
    }
    public Guid RequestKey
    {
        get; set;
    }
    public string ScopedKeyHash { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public long ReplayUntil
    {
        get; set;
    }
    public Guid AuditId
    {
        get; set;
    }
    public Guid GrantJobId
    {
        get; set;
    }
    public Guid GrantApprovalId
    {
        get; set;
    }
}
