using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Writes a fixed actor/target audit without serializing request payloads or credential material.</summary>
internal static class GovernanceAudit
{
    private const string User = "User";
    private const string Role = "Role";
    private const string Accepted = "Accepted";
    private const string Denied = "Denied";
    private const string AssignBundle = "AdministratorBundleAssigned";
    private const string RemoveBundle = "AdministratorBundleRemoved";
    private const string AddGrant = "AdministratorGrantAdded";
    private const string RemoveGrant = "AdministratorGrantRemoved";
    private const string EnableAccount = "AdministratorEnabled";
    private const string DisableAccount = "AdministratorDisabled";
    private const string SuspendAccount = "AdministratorSuspended";
    private const string ResumeAccount = "AdministratorResumed";

    /// <summary>Derives all security-relevant audit fields from validated immutable command intent.</summary>
    internal static void Add(PlatformDbContext database, GovernanceRequest request, string actorId, bool accepted, long now) =>
        database.Add(new SecurityAuditIntent
        {
            Event = Event(request.Action),
            ActorType = User,
            ActorId = actorId,
            TargetType = request.UserId is null ? Role : User,
            TargetId = request.UserId ?? request.RoleId,
            Outcome = accepted ? Accepted : Denied,
            OccurredAt = now
        });

    /// <summary>Maps only the fixed supported actions; no caller-provided event name reaches persistence.</summary>
    private static string Event(GovernanceAction action) => action switch
    {
        GovernanceAction.AssignBundle => AssignBundle,
        GovernanceAction.RemoveBundle => RemoveBundle,
        GovernanceAction.AddGrant => AddGrant,
        GovernanceAction.RemoveGrant => RemoveGrant,
        GovernanceAction.EnableAccount => EnableAccount,
        GovernanceAction.DisableAccount => DisableAccount,
        GovernanceAction.SuspendAccount => SuspendAccount,
        GovernanceAction.ResumeAccount => ResumeAccount,
        _ => throw new FactorUnavailableException()
    };
}
