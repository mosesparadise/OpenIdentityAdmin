namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Retains the native scope and original audience identity until the owning interaction becomes eligible for safe deletion.</summary>
internal sealed class ManagedScopeReference
{
    public string SelectorDigest { get; set; } = string.Empty;
    public string ScopeId { get; set; } = string.Empty;
    public string? AudienceId
    {
        get; set;
    }
}
