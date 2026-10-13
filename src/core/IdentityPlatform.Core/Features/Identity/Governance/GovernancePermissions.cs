using System.Collections.Frozen;

namespace IdentityPlatform.Core.Features.Identity.Governance;

/// <summary>Maps each supported transition to the current persisted permissions it requires.</summary>
/// <example><code>var required = GovernancePermissions.Required(GovernanceAction.DisableAccount);</code></example>
public static class GovernancePermissions
{
    private const string AssignAdministrators = "administrators.assign";
    private const string WriteUsers = "users.write";
    private const string DisableUsers = "users.disable";
    private const string ChangeGrants = "roles.permissions";
    private static readonly FrozenSet<string> Membership = new[] { AssignAdministrators, WriteUsers }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenSet<string> Grants = new[] { AssignAdministrators, ChangeGrants }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenSet<string> Account = new[] { AssignAdministrators, DisableUsers }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Returns immutable required permissions; an unsupported action is never mapped to an empty requirement.</summary>
    /// <param name="action">A supported internal governance action.</param>
    /// <returns>The ordinal permission set that must be present in current storage.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The action is not supported.</exception>
    /// <example><code>bool permitted = GovernancePermissions.Required(action).IsSubsetOf(currentGrants);</code></example>
    public static FrozenSet<string> Required(GovernanceAction action) => action switch
    {
        GovernanceAction.AssignBundle or GovernanceAction.RemoveBundle => Membership,
        GovernanceAction.AddGrant or GovernanceAction.RemoveGrant => Grants,
        GovernanceAction.EnableAccount or GovernanceAction.DisableAccount or
            GovernanceAction.SuspendAccount or GovernanceAction.ResumeAccount => Account,
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };
}
