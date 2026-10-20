using IdentityPlatform.Administrator.Tests.Features.Governance.Policy;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Declares native provider cases explicitly for complete discovery and isolated current-authority evidence.</summary>
/// <example><code>[MemberData(nameof(AdmissionCases.Bundles), MemberType = typeof(AdmissionCases))]</code></example>
public static class AdmissionCases
{
    internal const string Before = "before-default-deadline";
    internal const string Equality = "at-default-deadline";
    internal const string After = "after-default-deadline";
    internal const string ShortBefore = "before-short-deadline";
    internal const string ShortEquality = "at-short-deadline";
    internal const string Zero = "zero-age";
    internal const string Wider = "wider-age-cannot-extend";
    internal const string Rollback = "clock-rollback";
    internal const string Disabled = "disabled-account";
    internal const string Suspended = "suspended-enrollment";
    internal const string MissingFactor = "missing-active-factor";
    internal const string NoGrants = "no-current-grants";
    internal const string Authentication = "changed-authentication-generation";
    internal const string Revoked = "revoked-session";
    internal const string Factor = "changed-factor-generation";

    public static IEnumerable<TheoryDataRow<string, string>> Bundles => GovernanceRows.For(GrantCases.Platform, GrantCases.Application, GrantCases.Identity, GrantCases.Auditor);
    public static IEnumerable<TheoryDataRow<string, string>> Time => GovernanceRows.For(Before, Equality, After, ShortBefore, ShortEquality, Zero, Wider, Rollback);
    public static IEnumerable<TheoryDataRow<string, string>> Drift => GovernanceRows.For(Disabled, Suspended, MissingFactor, NoGrants, Authentication, Revoked, Factor);
    public static IEnumerable<TheoryDataRow<string, string>> ResponseFields => GovernanceRows.For(NativeAccountResponseFault.AuthorizationRefresh,
        NativeAccountResponseFault.AuthorizationIdentity, NativeAccountResponseFault.TokenRefresh, NativeAccountResponseFault.TokenIdentity);
    public static IEnumerable<TheoryDataRow<string, string>> Sessions => GovernanceRows.For(GovernanceSessionDamage.Missing, GovernanceSessionDamage.Revoked,
        GovernanceSessionDamage.DifferentUser, GovernanceSessionDamage.AuthenticationGeneration, GovernanceSessionDamage.FactorGeneration,
        GovernanceSessionDamage.SecurityStamp, GovernanceSessionDamage.Idle, GovernanceSessionDamage.Absolute, GovernanceSessionDamage.Recovery);
}
