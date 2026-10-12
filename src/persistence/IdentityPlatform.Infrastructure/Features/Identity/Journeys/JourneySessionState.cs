using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Shares read-only current-authority checks between ordinary validation and serialized mutations.</summary>
internal static class JourneySessionState
{
    /// <summary>Checks every current security binding and time boundary without clearing expired failure state.</summary>
    internal static bool Allows(PlatformUser user, AdministratorEnrollment enrollment, AdministratorAuthentication authentication,
        AdministratorSession session, long now) =>
        user.IsEnabled && user.TwoFactorEnabled && user.LockoutEnabled && !string.IsNullOrEmpty(user.SecurityStamp) &&
        !enrollment.Suspended && enrollment.State == AdministratorPolicy.Active && enrollment.FactorGeneration > 0 &&
        CeremonyBudget.IsAvailable(user, authentication, now) &&
        !session.Revoked && session.AuthenticationMethods == AdministratorPolicy.Methods &&
        session.Generation == authentication.Generation && session.FactorGeneration == enrollment.FactorGeneration &&
        session.SecurityStamp == user.SecurityStamp && session.PasswordVerifiedAt >= 0 &&
        session.FactorVerifiedAt >= session.PasswordVerifiedAt && session.CreatedAt >= session.FactorVerifiedAt &&
        session.CreatedAt <= now && session.LastActivityAt >= session.CreatedAt &&
        session.LastActivityAt <= now + AdministratorPolicy.ClockSkewSeconds &&
        session.LastActivityAt > now - AdministratorPolicy.IdleSeconds && session.ExpiresAt > now &&
        session.ExpiresAt == session.CreatedAt + AdministratorPolicy.SessionSeconds;
}
