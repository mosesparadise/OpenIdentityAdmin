namespace IdentityPlatform.Core.Features.Identity.Authentication;

/// <summary>Defines reviewed administrator security ceilings; configuration cannot relax these invariants.</summary>
/// <example><code>var expires = verifiedAt + AdministratorPolicy.InteractionSeconds;</code></example>
public static class AdministratorPolicy
{
    public const int InteractionSeconds = 300;
    public const int FailedAttempts = 5;
    public const int LockoutSeconds = 300;
    public const int FreshnessSeconds = 300;
    public const int IdleSeconds = 1800;
    public const int SessionSeconds = 28800;
    public const int ReplaySeconds = 300;
    public const int ClockSkewSeconds = 30;
    public const int CodeDigits = 6;
    public const int LiveInteractions = 8;
    public const int LiveSessions = 8;
    public const int RetainedUserSessions = 32;
    public const int RetainedRows = 10000;
    public const int ReplayEntries = 64;
    public const int CleanupRows = 100;
    public const int CleanupSeconds = 60;
    public const int OperationSeconds = 5;
    public const string Pending = "Pending";
    public const string Active = "Active";
    public const string RecoveryRequired = "RecoveryRequired";
    public const string Consumed = "Consumed";
    public const string Enrollment = "Enrollment";
    public const string Login = "Login";
    public const string Recovery = "Recovery";
    public const string Replacement = "Replacement";
    public const string Regeneration = "Regeneration";
    public const string Methods = "pwd otp";
}
