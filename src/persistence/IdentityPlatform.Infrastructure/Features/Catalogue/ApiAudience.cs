namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Stores product audience metadata without adding OAuth credentials or a second native resource mapping.</summary>
internal sealed class ApiAudience
{
    public string Id { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public long Revision { get; set; } = 1;
}
