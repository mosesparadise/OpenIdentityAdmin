namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Names exactly the reviewed population equivalence classes, independently of production policy branches.</summary>
internal static class PopulationCases
{
    internal const string MissingUser = "missing-user";
    internal const string DisabledUser = "disabled-user";
    internal const string NativeTwoFactorFalse = "native-twofactor-false";
    internal const string MissingPassword = "missing-password";
    internal const string EmptySecurityStamp = "empty-security-stamp";
    internal const string NativeLockoutDisabled = "native-lockout-disabled";
    internal const string MissingEnrollment = "missing-enrollment";
    internal const string PendingEnrollment = "pending-enrollment";
    internal const string ConsumedEnrollment = "consumed-enrollment";
    internal const string RecoveryEnrollment = "recovery-enrollment";
    internal const string UnknownEnrollment = "unknown-enrollment";
    internal const string SuspendedEnrollment = "suspended-enrollment";
    internal const string ZeroFactorGeneration = "zero-factor-generation";
    internal const string NegativeEnrollmentRevision = "negative-enrollment-revision";
    internal const string MissingAuthentication = "missing-authentication";
    internal const string ZeroAuthenticationGeneration = "zero-authentication-generation";
    internal const string NegativeAuthenticationRevision = "negative-authentication-revision";
    internal const string NegativeFailureCount = "negative-failure-count";
    internal const string InvalidFailureWindow = "invalid-failure-window";
    internal const string NativeLockout = "native-lockout";
    internal const string ProductLockout = "product-lockout";
    internal const string ClockFence = "clock-fence";
    internal const string InvalidCurrentTime = "invalid-current-time";
    internal const string MissingActiveFactor = "missing-active-factor";
    internal const string CorruptActiveFactor = "corrupt-active-factor";
    internal const string TransplantedFactor = "transplanted-factor";
    internal const string StaleFactorGeneration = "stale-factor-generation";
    internal const string MissingProtectionKey = "missing-protection-key";
    internal const string MissingPlatformMembership = "missing-platform-membership";
    internal const string MissingPlatformGrantWithOverlap = "missing-platform-grant-with-overlap";
    internal const string MissingActive = "missing-active";
    internal const string CorruptActive = "corrupt-active";
    internal const string LoggedOut = "logged-out";
    internal const string SessionExpired = "session-expired";
    internal const string Staged = "staged";
    internal const string Recovery = "recovery";
    internal const string Installation = "installation";
    internal static readonly string[] MissingConditions =
    [
        MissingUser, DisabledUser, NativeTwoFactorFalse, MissingPassword, EmptySecurityStamp,
        NativeLockoutDisabled, MissingEnrollment, PendingEnrollment, ConsumedEnrollment, RecoveryEnrollment,
        UnknownEnrollment, SuspendedEnrollment, ZeroFactorGeneration, NegativeEnrollmentRevision,
        MissingAuthentication, ZeroAuthenticationGeneration, NegativeAuthenticationRevision, NegativeFailureCount,
        InvalidFailureWindow, NativeLockout, ProductLockout, ClockFence, InvalidCurrentTime, MissingActiveFactor,
        CorruptActiveFactor, TransplantedFactor, StaleFactorGeneration, MissingProtectionKey,
        MissingPlatformMembership, MissingPlatformGrantWithOverlap
    ];

    /// <summary>Separates schema-prohibited states from persisted policy observations; a constraint pass is never called policy coverage.</summary>
    internal static bool RequiresConstraint(string row) => row is MissingEnrollment or MissingAuthentication or
        NegativeEnrollmentRevision or ZeroAuthenticationGeneration or NegativeAuthenticationRevision or NegativeFailureCount;

    /// <summary>Names states whose unavailable native material rejects the whole observation through the public internal boundary.</summary>
    internal static bool RequiresUnavailable(string row) => row is MissingUser or EmptySecurityStamp or
        MissingActiveFactor or CorruptActiveFactor or TransplantedFactor or StaleFactorGeneration or MissingProtectionKey;
}
