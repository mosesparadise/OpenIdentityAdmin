using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Records finite managed human events in the same transaction without credentials, selectors or form payloads.</summary>
internal static class ManagedHumanAudit
{
    private const string Password = "managed-password";
    private const string Mfa = "managed-mfa";
    private const string Consent = "managed-consent";
    private const string Account = "account";
    private const string Anonymous = "anonymous";
    private const string Application = "application";
    private const string Accepted = "accepted";
    private const string Denied = "denied";

    /// <summary>Stages one exact event whose nonsecret target and correlation are server-owned interaction metadata.</summary>
    internal static SecurityAuditIntent? Add(PlatformDbContext database, ManagedLoginInteraction row, ManagedHumanAction action, string? user, bool accepted)
    {
        if (action == ManagedHumanAction.Read)
        {
            return null;
        }
        var audit = new SecurityAuditIntent
        {
            Event = action switch { ManagedHumanAction.Password => Password, ManagedHumanAction.Mfa => Mfa, _ => Consent },
            ActorType = user is null ? Anonymous : Account,
            ActorId = user,
            TargetType = Application,
            TargetId = row.ApplicationId,
            Outcome = accepted ? Accepted : Denied,
            CorrelationId = row.CorrelationId,
            OccurredAt = row.ObservedAt
        };
        database.Add(audit);
        return audit;
    }
}
