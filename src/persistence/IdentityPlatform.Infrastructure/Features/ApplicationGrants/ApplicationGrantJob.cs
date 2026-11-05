using IdentityPlatform.Core.Features.ApplicationGrants;

namespace IdentityPlatform.Infrastructure.Features.ApplicationGrants;

/// <summary>Persists committed progress for one disabled application's fixed native revocation scope.</summary>
internal sealed class ApplicationGrantJob
{
    public Guid Id
    {
        get; set;
    }
    public string ApplicationId { get; set; } = string.Empty;
    public long ApplicationRevision
    {
        get; set;
    }
    public Guid EffectiveApprovalId
    {
        get; set;
    }
    public GrantJobState State { get; set; } = GrantJobState.Queued;
    public GrantJobFailure Failure
    {
        get; set;
    }
    public long Revision { get; set; } = 1;
    public long AuthorizationCount
    {
        get; set;
    }
    public long TokenCount
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
}
