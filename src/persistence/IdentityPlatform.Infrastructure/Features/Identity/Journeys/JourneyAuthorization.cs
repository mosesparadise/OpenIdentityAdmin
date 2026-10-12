using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Separates password-only interactions from native-authorized replacement stages and current-session actions.</summary>
internal static class JourneyAuthorization
{
    internal const string RecoveryProof = "recovery-code";
    internal const string FactorProof = "current-otp";

    /// <summary>Identifies actions that require a current stored full session before password admission.</summary>
    internal static bool IsVoluntary(string purpose) => purpose is AdministratorPolicy.Replacement or AdministratorPolicy.Regeneration;

    /// <summary>Requires native session validation only for explicitly voluntary actions.</summary>
    internal static Task<JourneySessionProof?> SessionAsync(PlatformDbContext database, UserManager<PlatformUser> users,
        CeremonyClock clock, CeremonyTransaction transaction, CeremonyAccount account, string purpose, JourneySessionCandidate? candidate) =>
        IsVoluntary(purpose) ? JourneySessionProof.ReadAsync(database, users, clock, transaction, account, candidate) : Task.FromResult<JourneySessionProof?>(null);

    /// <summary>Checks persisted proof provenance and allocated destination; null legacy state never grants staging authority.</summary>
    internal static bool IsStaged(CeremonyAccount account, AdministratorInteraction row) =>
        row.Purpose == AdministratorPolicy.Replacement && !string.IsNullOrEmpty(row.StagedSeed) && row.ExpectedEnrollmentRevision == account.Enrollment.Revision &&
        row.DestinationGeneration > account.Enrollment.FactorGeneration &&
        row.DestinationGeneration <= account.Authentication.LastAllocatedFactorGeneration &&
        row.SourceFactorGeneration == account.Enrollment.FactorGeneration && row.AuthorizationVerifiedAt >= row.PasswordVerifiedAt &&
        row.AuthorizationVerifiedAt < row.ExpiresAt && account.User.IsEnabled && account.User.TwoFactorEnabled && !account.Enrollment.Suspended &&
        ((row.AuthorizationKind == RecoveryProof && row.SessionPublicId is null && account.Enrollment.State == AdministratorPolicy.RecoveryRequired) ||
         (row.AuthorizationKind == FactorProof && CeremonySecrets.IsIdentifier(row.SessionPublicId) && account.IsActive));

    /// <summary>Rejects future or expired step-up evidence without extending the original password or interaction lifetime.</summary>
    internal static bool IsFresh(AdministratorInteraction row, long now) => row.AuthorizationVerifiedAt is { } verifiedAt &&
        verifiedAt <= now && now - verifiedAt < AdministratorPolicy.FreshnessSeconds;

    /// <summary>Requires the incoming browser cookie to match the bound initiating session, then rereads its current authority.</summary>
    internal static Task<JourneySessionProof?> BoundSessionAsync(PlatformDbContext database, UserManager<PlatformUser> users,
        CeremonyClock clock, CeremonyTransaction transaction, CeremonyAccount account, AdministratorInteraction row, JourneySessionCandidate? candidate) =>
        candidate is not null && candidate.SessionId == row.SessionPublicId
            ? JourneySessionProof.ReadAsync(database, users, clock, transaction, account, candidate)
            : Task.FromResult<JourneySessionProof?>(null);

    /// <summary>Recognizes only the authorized recovery stage that intentionally carries no full-session binding.</summary>
    internal static bool IsRecoveryStage(AdministratorInteraction row) =>
        row.Purpose == AdministratorPolicy.Replacement && row.AuthorizationKind == RecoveryProof && row.SessionPublicId is null;
}
