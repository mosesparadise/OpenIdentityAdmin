namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Retains bounded original deletion approval and terminal state independently of the deleted catalogue row.</summary>
internal sealed class CatalogueDeletion
{
    public Guid Id
    {
        get; set;
    }
    public string Kind { get; set; } = string.Empty;
    public string TargetId { get; set; } = string.Empty;
    public long TargetRevision
    {
        get; set;
    }
    public string ActorId { get; set; } = string.Empty;
    public string SecurityStamp { get; set; } = string.Empty;
    public long AuthenticationGeneration
    {
        get; set;
    }
    public long FactorGeneration
    {
        get; set;
    }
    public long EnrollmentRevision
    {
        get; set;
    }
    public long FactorVerifiedAt
    {
        get; set;
    }
    public long ApprovalUntil
    {
        get; set;
    }
    public long CreatedAt
    {
        get; set;
    }
    public long ObservedAt
    {
        get; set;
    }
    public long Revision { get; set; } = 1;
    public Guid RequestKey
    {
        get; set;
    }
    public string ScopedKeyHash { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public Guid AdmissionAuditId
    {
        get; set;
    }
    public Guid? CompletionAuditId
    {
        get; set;
    }
    public CatalogueDeletionState State { get; set; } = CatalogueDeletionState.Queued;
    public CatalogueDeletionFailure Failure
    {
        get; set;
    }
}

/// <summary>Separates queued intent from actual committed deletion or terminal denial; no transient in-memory lease claims success.</summary>
internal enum CatalogueDeletionState
{
    Queued = 1, Succeeded = 2, Rejected = 3
}

/// <summary>Records only safe finite reasons; native exception text and personally identifying data never enter job errors.</summary>
internal enum CatalogueDeletionFailure
{
    None = 0, Stale = 1, Referenced = 2, Authority = 3, Expired = 4, Missing = 5
}
