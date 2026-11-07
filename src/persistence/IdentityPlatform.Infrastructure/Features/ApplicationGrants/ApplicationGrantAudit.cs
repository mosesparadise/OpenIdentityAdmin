using IdentityPlatform.Core.Features.ApplicationGrants;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Links each committed revision to exact progress and the approval that authorized that transaction.</summary>
internal sealed class ApplicationGrantAudit
{
    public Guid AuditId
    {
        get; set;
    }
    public Guid JobId
    {
        get; set;
    }
    public Guid ApprovalId
    {
        get; set;
    }
    public long JobRevision
    {
        get; set;
    }
    public long AuthorizationDelta
    {
        get; set;
    }
    public long TokenDelta
    {
        get; set;
    }
    public long AuthorizationCount
    {
        get; set;
    }
    public long TokenCount
    {
        get; set;
    }
    public GrantJobState State
    {
        get; set;
    }
    public GrantJobFailure Failure
    {
        get; set;
    }
}
