using IdentityPlatform.Core.Features.Identity.Permissions;

namespace IdentityPlatform.Core.Features.Identity.Governance;

/// <summary>Copies and validates immutable command intent before discovery; it carries no authentication authority.</summary>
/// <remarks>Credentials, session proof and server-derived witness identities never belong in this value.</remarks>
/// <example><code>
/// var request = GovernanceRequest.Create(GovernanceAction.DisableAccount, versions, userId: targetId);
/// // Null denotes an invalid shape, not authorization failure or successful execution.
/// </code></example>
public sealed class GovernanceRequest
{
    private const int NativeIdentifierLength = 450;
    private const int NativeStampLength = 450;
    public GovernanceAction Action
    {
        get;
    }
    public GovernanceVersions Versions
    {
        get;
    }
    public string? UserId
    {
        get;
    }
    public string? RoleId
    {
        get;
    }
    public string? Permission
    {
        get;
    }

    /// <summary>Captures only the immutable values that passed the action-specific shape validation.</summary>
    private GovernanceRequest(GovernanceAction action, GovernanceVersions versions, string? userId, string? roleId, string? permission)
    {
        Action = action;
        Versions = versions with
        {
        };
        UserId = userId;
        RoleId = roleId;
        Permission = permission;
    }

    /// <summary>Rejects unsupported commands, noncanonical identifiers and irrelevant or missing target versions.</summary>
    /// <returns>An immutable command or null for invalid input; no database access occurs.</returns>
    /// <example><code>var request = GovernanceRequest.Create(action, versions, userId, roleId, permission);</code></example>
    public static GovernanceRequest? Create(GovernanceAction action, GovernanceVersions? versions,
        string? userId = null, string? roleId = null, string? permission = null)
    {
        if (!Enum.IsDefined(action) || versions is null || versions.AuthorityRevision < 0)
        {
            return null;
        }
        var membership = action is GovernanceAction.AssignBundle or GovernanceAction.RemoveBundle;
        var grant = action is GovernanceAction.AddGrant or GovernanceAction.RemoveGrant;
        if (!ValidUser(versions, userId, !grant) || !ValidRole(versions, roleId, membership || grant) ||
            (grant ? permission is null || !AdministratorPermissions.Bundles[AdministratorPermissions.PlatformRole].Contains(permission) : permission is not null))
        {
            return null;
        }
        return new GovernanceRequest(action, versions, userId, roleId, permission);
    }

    /// <summary>Requires both user version dimensions exactly when the action has a user target.</summary>
    private static bool ValidUser(GovernanceVersions versions, string? id, bool required) => required
        ? IsIdentifier(id) && IsCanonical(versions.UserStamp, NativeStampLength) && versions.EnrollmentRevision is >= 0
        : id is null && versions.UserStamp is null && versions.EnrollmentRevision is null;

    /// <summary>Requires an explicit native role stamp exactly when the action has a role target.</summary>
    private static bool ValidRole(GovernanceVersions versions, string? id, bool required) => required
        ? IsIdentifier(id) && IsCanonical(versions.RoleStamp, NativeStampLength)
        : id is null && versions.RoleStamp is null;

    /// <summary>Accepts bounded nonempty native identifiers without normalization or surrounding whitespace.</summary>
    /// <example><code>bool valid = GovernanceRequest.IsIdentifier(candidateId);</code></example>
    public static bool IsIdentifier(string? value) => IsCanonical(value, NativeIdentifierLength);

    /// <summary>Rejects whitespace padding and control characters rather than normalizing security identifiers.</summary>
    private static bool IsCanonical(string? value, int limit) => !string.IsNullOrEmpty(value) &&
        value.Length <= limit && value.AsSpan().Trim().SequenceEqual(value.AsSpan()) && !value.Any(char.IsControl);
}
