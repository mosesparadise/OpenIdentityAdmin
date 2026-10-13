namespace IdentityPlatform.Core.Features.Identity.Governance;

/// <summary>Identifies the finite internal administrator authority transitions.</summary>
/// <example><code>var action = GovernanceAction.RemoveBundle;</code></example>
public enum GovernanceAction
{
    AssignBundle,
    RemoveBundle,
    AddGrant,
    RemoveGrant,
    EnableAccount,
    DisableAccount,
    SuspendAccount,
    ResumeAccount
}
