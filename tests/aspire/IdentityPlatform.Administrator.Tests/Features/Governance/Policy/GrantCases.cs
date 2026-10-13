using IdentityPlatform.Administrator.Tests.Fixtures.Governance;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Policy;

/// <summary>Supplies the exact predeclared provider cases and independent finite permission expectations.</summary>
/// <example><code>[MemberData(nameof(GrantCases.Bundles), MemberType = typeof(GrantCases))]</code></example>
public static class GrantCases
{
    internal const string OverlapCase = "overlap";
    internal const string UserClaim = "user-claim";
    internal const string PrincipalClaim = "principal-claim";
    internal const string CustomRole = "custom-role";
    internal const string RoleNameCase = "role-name-case";
    internal const string RoleNamePadding = "role-name-padding";
    internal const string NormalizedRoleAlias = "normalized-role-alias";
    internal const string RoleIdAlias = "role-id-alias";
    internal const string UserIdAlias = "user-id-alias";
    internal const string ClaimTypeCase = "claim-type-case";
    internal const string ClaimTypePadding = "claim-type-padding";
    internal const string ClaimValueCase = "claim-value-case";
    internal const string ClaimValuePadding = "claim-value-padding";
    internal const string Wildcard = "wildcard";
    internal const string UnknownGrant = "unknown-grant";
    internal const string OutOfBundleGrant = "out-of-bundle-grant";
    internal const string DuplicateRoleGrant = "duplicate-role-grant";
    internal const string CustomRolePermission = "custom-role-permission";
    internal const string AmbiguousReservedIdentity = "ambiguous-reserved-identity";
    internal const string DuplicateMembership = "duplicate-membership";
    internal const string CrossStoreRemoval = "cross-store-removal";
    internal const string UnknownRequestCase = "unknown-request";
    internal const string Platform = "PlatformAdministrator";
    internal const string Application = "ApplicationAdministrator";
    internal const string Identity = "IdentityAdministrator";
    internal const string Auditor = "Auditor";
    internal const string ClaimType = "identityplatform.permission";
    internal const string UserRead = "users.read";
    internal const string Unknown = "unknown.permission";
    internal const string CustomRoleName = "UnprivilegedCustomRole";
    internal const string UnrelatedType = "test.unrelated";
    internal const string UnrelatedValue = "ignored";
    internal const string PostgreSql = "PostgreSql";
    internal const string SqlServer = "SqlServer";
    public static IEnumerable<TheoryDataRow<string, string>> Bundles => GovernanceRows.For(Platform, Application, Identity, Auditor);
    public static IEnumerable<TheoryDataRow<string, string>> Overlap => GovernanceRows.For(GrantCases.OverlapCase);
    public static IEnumerable<TheoryDataRow<string, string>> NonAuthority => GovernanceRows.For(GrantCases.UserClaim, GrantCases.PrincipalClaim, GrantCases.CustomRole);
    public static IEnumerable<TheoryDataRow<string, string>> Malformed => GovernanceRows.For(GrantCases.RoleNameCase, GrantCases.RoleNamePadding, GrantCases.NormalizedRoleAlias,
        GrantCases.RoleIdAlias, GrantCases.UserIdAlias, GrantCases.ClaimTypeCase, GrantCases.ClaimTypePadding, GrantCases.ClaimValueCase, GrantCases.ClaimValuePadding, GrantCases.Wildcard,
        GrantCases.UnknownGrant, GrantCases.OutOfBundleGrant, GrantCases.DuplicateRoleGrant, GrantCases.CustomRolePermission, GrantCases.AmbiguousReservedIdentity);
    public static IEnumerable<TheoryDataRow<string, string>> Duplicate => GovernanceRows.For(GrantCases.DuplicateMembership);
    public static IEnumerable<TheoryDataRow<string, string>> Removal => GovernanceRows.For(GrantCases.CrossStoreRemoval);
    public static IEnumerable<TheoryDataRow<string, string>> UnknownRequest => GovernanceRows.For(GrantCases.UnknownRequestCase);

    private static readonly string[] PlatformPermissions = ["applications.read", "applications.write", "applications.credentials", "applications.disable", "scopes.read", "scopes.write",
            "audiences.read", "audiences.write", "configuration.read", "configuration.evaluate", "users.read", "users.write", "users.disable",
            "users.unlock", "users.recovery", "roles.read", "roles.write", "roles.permissions", "grants.read", "grants.revoke", "sessions.read",
            "sessions.revoke", "audit.read", "keys.read", "administrators.assign"];
    private static readonly string[] ApplicationPermissions = ["applications.read", "applications.write", "applications.credentials", "applications.disable", "scopes.read", "scopes.write",
            "audiences.read", "audiences.write", "configuration.read", "configuration.evaluate"];
    private static readonly string[] IdentityPermissions = ["users.read", "users.write", "users.disable", "users.unlock", "users.recovery", "roles.read", "roles.write", "roles.permissions",
            "grants.read", "grants.revoke", "sessions.read", "sessions.revoke"];
    private static readonly string[] AuditorPermissions = ["applications.read", "scopes.read", "audiences.read", "users.read", "roles.read", "grants.read", "sessions.read", "audit.read",
            "configuration.read", "keys.read"];

    /// <summary>Returns independent literal expectations without querying production catalogue defaults or grant evaluation.</summary>
    internal static string[] Expected(string bundle) => bundle switch
    {
        Platform => PlatformPermissions,
        Application => ApplicationPermissions,
        Identity => IdentityPermissions,
        Auditor => AuditorPermissions,
        _ => throw new ArgumentOutOfRangeException(nameof(bundle))
    };
}
