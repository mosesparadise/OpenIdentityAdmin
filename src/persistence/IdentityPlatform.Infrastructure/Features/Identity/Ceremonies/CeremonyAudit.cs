using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

internal enum CeremonyEvent
{
    Password, Factor, Enrollment, Recovery, Replacement, Regeneration, Logout
}

/// <summary>Restricts durable ceremony diagnostics to a fixed event catalogue and nonsecret account identifiers.</summary>
internal static class CeremonyAudit
{
    private const string Actor = "account";
    private const string Target = "administrator-account";
    private const string Accepted = "accepted";
    private const string Denied = "denied";
    private const string Password = "administrator-password";
    private const string Factor = "administrator-factor";
    private const string Enrollment = "administrator-enrollment";
    private const string Recovery = "administrator-recovery";
    private const string Replacement = "administrator-replacement";
    private const string Regeneration = "administrator-regeneration";
    private const string Logout = "administrator-logout";

    /// <summary>Stages a sanitized audit in the same context; callers cannot pass a secret-bearing event or message.</summary>
    internal static void Add(PlatformDbContext database, CeremonyEvent action, bool accepted, string userId, long now)
    {
        database.Add(new SecurityAuditIntent
        {
            Event = action switch
            {
                CeremonyEvent.Password => Password,
                CeremonyEvent.Factor => Factor,
                CeremonyEvent.Enrollment => Enrollment,
                CeremonyEvent.Recovery => Recovery,
                CeremonyEvent.Replacement => Replacement,
                CeremonyEvent.Regeneration => Regeneration,
                CeremonyEvent.Logout => Logout,
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            },
            ActorType = Actor,
            ActorId = userId,
            TargetType = Target,
            TargetId = userId,
            Outcome = accepted ? Accepted : Denied,
            OccurredAt = now
        });
    }
}
