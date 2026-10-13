using System.Collections.Frozen;

namespace IdentityPlatform.Core.Features.Identity.Permissions;

/// <summary>Defines the versioned, case-sensitive role bundles used to seed administrator permissions.</summary>
/// <remarks>
/// This catalogue describes bootstrap defaults. Membership in a catalogue bundle does not establish a
/// caller's current authority; authorization must use the persisted grants and applicable account/session policy.
/// Bundle and permission identifiers are stable stored values, compared with ordinal semantics.
/// </remarks>
/// <example>
/// Inspect the default platform-administrator bundle when preparing bootstrap data:
/// <code>
/// var defaults = AdministratorPermissions.Bundles[AdministratorPermissions.PlatformRole];
/// var orderedPermissions = defaults.Order(StringComparer.Ordinal).ToArray();
/// </code>
/// This reads seed metadata only; it is not a request-authorization check.
/// </example>
public static class AdministratorPermissions
{
    /// <summary>Identifies persisted role claims that carry administrator permission values.</summary>
    public const string ClaimType = "identityplatform.permission";

    /// <summary>Names the bootstrap role containing every default administrator permission.</summary>
    public const string PlatformRole = "PlatformAdministrator";

    /// <summary>Identifies the permission-bundle contract recorded by administrator bootstrap.</summary>
    public const int BundleVersion = 1;

    private const string ApplicationRole = "ApplicationAdministrator";
    private const string IdentityRole = "IdentityAdministrator";
    private const string AuditorRole = "Auditor";
    private const string AssignAdministratorsPermission = "administrators.assign";

    private static readonly FrozenSet<string> ApplicationAdministratorPermissions = new[]
    {
        "applications.read", "applications.write", "applications.credentials", "applications.disable",
        "scopes.read", "scopes.write", "audiences.read", "audiences.write",
        "configuration.read", "configuration.evaluate"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> IdentityAdministratorPermissions = new[]
    {
        "users.read", "users.write", "users.disable", "users.unlock", "users.recovery",
        "roles.read", "roles.write", "roles.permissions", "grants.read", "grants.revoke",
        "sessions.read", "sessions.revoke"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> AuditorPermissions = new[]
    {
        "applications.read", "scopes.read", "audiences.read", "users.read", "roles.read",
        "grants.read", "sessions.read", "audit.read", "configuration.read", "keys.read"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Gets immutable default bundles keyed by their exact persisted role names.</summary>
    /// <remarks>The platform bundle is the distinct union of the three specialist bundles plus administrator assignment.</remarks>
    public static FrozenDictionary<string, FrozenSet<string>> Bundles { get; } = CreateBundles();

    /// <summary>Freeze the specialist bundles and their platform union using case-sensitive ordinal comparison.</summary>
    private static FrozenDictionary<string, FrozenSet<string>> CreateBundles()
    {
        var bundles = new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
        {
            [ApplicationRole] = ApplicationAdministratorPermissions,
            [IdentityRole] = IdentityAdministratorPermissions,
            [AuditorRole] = AuditorPermissions
        };
        bundles[PlatformRole] = bundles.Values.SelectMany(value => value).Append(AssignAdministratorsPermission).ToFrozenSet(StringComparer.Ordinal);
        return bundles.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
