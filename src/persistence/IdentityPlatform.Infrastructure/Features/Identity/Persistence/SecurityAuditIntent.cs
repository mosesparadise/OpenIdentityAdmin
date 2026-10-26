namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Stores a finite security event and nonsecret operation provenance in the owning transaction.</summary>
/// <example><code>var audit = new SecurityAuditIntent { Event = "example-action", ActorType = "account", TargetType = "account", Outcome = "accepted", OccurredAt = 1 };</code></example>
public sealed class SecurityAuditIntent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Event
    {
        get; set;
    }
    public required string ActorType
    {
        get; set;
    }
    public string? ActorId
    {
        get; set;
    }
    public required string TargetType
    {
        get; set;
    }
    public string? TargetId
    {
        get; set;
    }
    public required string Outcome
    {
        get; set;
    }
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    /// <summary>Identifies related source history without asserting that two operations share an outcome.</summary>
    public Guid? RelatedCorrelationId
    {
        get; set;
    }
    public long OccurredAt
    {
        get; set;
    }
}
