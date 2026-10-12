using System.Collections.Frozen;

namespace IdentityPlatform.Core.Features.Identity.Permissions;

public static class AdministratorPermissions
{
    public const string ClaimType = "identityplatform.permission";
    public const string PlatformRole = "PlatformAdministrator";
    public const int BundleVersion = 1;

    private static readonly FrozenSet<string> ApplicationAdministratorPermissions = new[] { "applications.read", "applications.write", "applications.credentials", "applications.disable", "scopes.read", "scopes.write", "audiences.read", "audiences.write", "configuration.read", "configuration.evaluate" }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenSet<string> IdentityAdministratorPermissions = new[] { "users.read", "users.write", "users.disable", "users.unlock", "users.recovery", "roles.read", "roles.write", "roles.permissions", "grants.read", "grants.revoke", "sessions.read", "sessions.revoke" }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly FrozenSet<string> AuditorPermissions = new[] { "applications.read", "scopes.read", "audiences.read", "users.read", "roles.read", "grants.read", "sessions.read", "audit.read", "configuration.read", "keys.read" }.ToFrozenSet(StringComparer.Ordinal);

    public static FrozenDictionary<string, FrozenSet<string>> Bundles { get; } = CreateBundles();

    private static FrozenDictionary<string, FrozenSet<string>> CreateBundles()
    {
        var bundles = new Dictionary<string, FrozenSet<string>>(StringComparer.Ordinal)
        {
            ["ApplicationAdministrator"] = ApplicationAdministratorPermissions,
            ["IdentityAdministrator"] = IdentityAdministratorPermissions,
            ["Auditor"] = AuditorPermissions
        };
        bundles[PlatformRole] = bundles.Values.SelectMany(value => value).Append("administrators.assign").ToFrozenSet(StringComparer.Ordinal);
        return bundles.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
