namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Binds finite product disclosure policy to one explicitly managed native scope; native resources remain authoritative.</summary>
internal sealed class ScopePolicy
{
    public string ScopeId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string ConsentText { get; set; } = string.Empty;
    public string Disclosure { get; set; } = string.Empty;
    public long Revision { get; set; } = 1;
}
