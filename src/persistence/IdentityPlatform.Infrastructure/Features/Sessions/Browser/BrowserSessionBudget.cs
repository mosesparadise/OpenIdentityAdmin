namespace IdentityPlatform.Infrastructure.Features.Sessions.Browser;

/// <summary>Serializes retained capacity and browser writers across application instances using a native transactional update.</summary>
internal sealed class BrowserSessionBudget
{
    public int Id { get; set; } = 1;
    public long Revision
    {
        get; set;
    }
}
