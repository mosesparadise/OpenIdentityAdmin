namespace IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;

/// <summary>Retains immutable ceremony references and replay tombstones independently of account cleanup; this row alone grants no authentication.</summary>
internal sealed class IssuerContinuation
{
    public string IntentDigest { get; set; } = string.Empty;
    public string BrowserDigest { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string State { get; set; } = IssuerContinuationPolicy.Pending;
    public long CreatedAt
    {
        get; set;
    }
    public long ExpiresAt
    {
        get; set;
    }
    public long ObservedAt
    {
        get; set;
    }
    public long RetainUntil
    {
        get; set;
    }
    public long Revision
    {
        get; set;
    }
    public Guid CorrelationId
    {
        get; set;
    }
    public string? InteractionId
    {
        get; set;
    }
    public string? InteractionBrowserDigest
    {
        get; set;
    }
    public string? BrowserBudgetId
    {
        get; set;
    }
    public string? UserId
    {
        get; set;
    }
    public string? SessionId
    {
        get; set;
    }
    public long? SessionExpiresAt
    {
        get; set;
    }
    public string? CompletionDigest
    {
        get; set;
    }
    public string? RequestId
    {
        get; set;
    }
}
