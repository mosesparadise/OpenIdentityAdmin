namespace IdentityPlatform.Infrastructure.Features.Sessions.Browser;

/// <summary>Records a finite atomic browser transition with optional authenticated ticket provenance, without retaining selectors.</summary>
internal sealed class BrowserSessionAudit
{
    public Guid Id
    {
        get; set;
    }
    public Guid CorrelationId
    {
        get; set;
    }
    public long Generation
    {
        get; set;
    }
    public long OccurredAt
    {
        get; set;
    }
    public BrowserMutation Operation
    {
        get; set;
    }
    public string? Subject
    {
        get; set;
    }
    public string? SourceSession
    {
        get; set;
    }
}
