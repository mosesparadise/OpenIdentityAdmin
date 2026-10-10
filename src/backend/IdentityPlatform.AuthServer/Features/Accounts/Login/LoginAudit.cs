using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.AuthServer.Features.Accounts.Login;

public static class LoginAudit
{
    public static void SessionReplaced(PlatformDbContext database, string previousSessionId, string userId, long now)
    {
        database.Add(new SecurityAuditIntent
        {
            Event = "session-replacement",
            Outcome = "revoked",
            ActorType = "account",
            ActorId = userId,
            TargetType = "local-session-digest",
            TargetId = InteractionSecrets.Digest(previousSessionId),
            OccurredAt = now
        });
    }

    public static void Add(PlatformDbContext database, string eventCode, string outcome, long now,
        string? userId = null, string actorType = "account")
    {
        database.Add(new SecurityAuditIntent
        {
            Event = eventCode,
            Outcome = outcome,
            ActorType = actorType,
            ActorId = actorType == "account" ? userId : null,
            TargetType = "pilot-account",
            TargetId = userId,
            OccurredAt = now
        });
    }
}
