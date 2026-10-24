namespace IdentityPlatform.Infrastructure.Features.Sessions.Browser;

/// <summary>Correlates a local committed logout with an unverified browser return; never supplies issuer authority.</summary>
internal sealed class BrowserLogoutContinuation
{
    public Guid CorrelationId
    {
        get; set;
    }
    public string Digest { get; set; } = string.Empty;
    public string BrowserDigest { get; set; } = string.Empty;
    public long Generation
    {
        get; set;
    }
    public long Revision
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
    public long ExpiresAt
    {
        get; set;
    }
    public long RetainUntil
    {
        get; set;
    }
    public BrowserLogoutState State
    {
        get; set;
    }
    public string Subject { get; set; } = string.Empty;
    public string SourceSession { get; set; } = string.Empty;
}
