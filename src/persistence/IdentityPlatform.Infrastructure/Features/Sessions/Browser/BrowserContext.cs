namespace IdentityPlatform.Infrastructure.Features.Sessions.Browser;

/// <summary>Orders operations for one anonymous browser proof without treating that proof as identity.</summary>
internal sealed class BrowserContext
{
    public Guid CorrelationId
    {
        get; set;
    }
    public string Digest { get; set; } = string.Empty;
    public long Generation
    {
        get; set;
    }
    public long Revision
    {
        get; set;
    }
    public long ObservedAt
    {
        get; set;
    }
    public long ExpiresAt
    {
        get; set;
    }
    public long RetainUntil
    {
        get; set;
    }
    public string? TicketDigest
    {
        get; set;
    }
}
