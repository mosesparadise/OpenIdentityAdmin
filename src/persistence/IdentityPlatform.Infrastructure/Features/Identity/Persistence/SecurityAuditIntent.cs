namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

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
    public long OccurredAt
    {
        get; set;
    }
}
