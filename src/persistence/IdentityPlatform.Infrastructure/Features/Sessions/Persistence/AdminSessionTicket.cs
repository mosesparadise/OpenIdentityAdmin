using System.Text.Json.Serialization;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Persistence;

/// <summary>Stores authenticated ticket metadata using portable full-precision UTC ticks and a protected native payload.</summary>
internal sealed class AdminSessionTicket
{
    public string Digest { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string SourceSession { get; set; } = string.Empty;
    [JsonIgnore]
    public byte[] ProtectedTicket { get; set; } = [];
    public long CreatedAt
    {
        get; set;
    }
    public long LastActivityAt
    {
        get; set;
    }
    public long AbsoluteExpiresAt
    {
        get; set;
    }
    public long IdleLifetime
    {
        get; set;
    }
    public long IdleExpiresAt
    {
        get; set;
    }
    public long Revision
    {
        get; set;
    }
    [JsonIgnore]
    public long? RevokedAt
    {
        get; set;
    }
}

/// <summary>Retains minimal atomic mutation intent independently of ticket cleanup; it does not attribute an authenticated actor.</summary>
internal sealed class AdminSessionAudit
{
    public Guid Id
    {
        get; set;
    }
    public Guid CorrelationId
    {
        get; set;
    }
    public long OccurredAt
    {
        get; set;
    }
    public SessionMutation Operation
    {
        get; set;
    }
    public SessionMutationOutcome Outcome
    {
        get; set;
    }
    public string Digest { get; set; } = string.Empty;
    public long Revision
    {
        get; set;
    }
}

/// <summary>Restricts stored transition intent to this store's implemented mutations.</summary>
internal enum SessionMutation
{
    Create = 1, Renew = 2, Revoke = 3
}

/// <summary>Only committed transitions have an intent row; failures never fabricate a successful audit event.</summary>
internal enum SessionMutationOutcome
{
    Committed = 1
}
