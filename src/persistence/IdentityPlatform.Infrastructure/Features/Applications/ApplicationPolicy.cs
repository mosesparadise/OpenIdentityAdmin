using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Stores product lifecycle and concurrency metadata beside the native application without duplicating native permissions or credentials.</summary>
internal sealed class ApplicationPolicy
{
    public string ApplicationId { get; set; } = string.Empty;
    public string Profile { get; set; } = ApplicationEditorRules.Profile;
    public long Revision
    {
        get; set;
    }
    public long CredentialRevision
    {
        get; set;
    }
    public long CredentialIssuedAt
    {
        get; set;
    }
    public long CredentialExpiresAt
    {
        get; set;
    }
    public long ObservedAt
    {
        get; set;
    }
}
