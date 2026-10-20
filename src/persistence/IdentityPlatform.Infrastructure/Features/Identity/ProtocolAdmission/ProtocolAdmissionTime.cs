using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;

namespace IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;

/// <summary>Derives strict original-proof deadlines without renewing authentication or widening a shorter requested age.</summary>
internal static class ProtocolAdmissionTime
{
    internal const int CodeSeconds = 60;
    internal const int TokenSeconds = 300;

    /// <summary>Requires both persisted proof ages to be nonnegative and strictly below the smaller approved ceiling.</summary>
    internal static bool Allows(AdministratorSession session, long now, long? maximumAge)
    {
        var age = maximumAge is { } requested ? Math.Min(requested, AdministratorPolicy.FreshnessSeconds) : AdministratorPolicy.FreshnessSeconds;
        return age > 0 && session.PasswordVerifiedAt >= 0 && session.FactorVerifiedAt >= session.PasswordVerifiedAt &&
            now >= session.FactorVerifiedAt && now - session.PasswordVerifiedAt < age && now - session.FactorVerifiedAt < age;
    }

    /// <summary>Bounds a new code by both original proof deadlines and the original absolute source-session expiry.</summary>
    internal static long CodeExpiry(AdministratorSession session, long now, long? maximumAge)
    {
        var age = maximumAge is { } requested ? Math.Min(requested, AdministratorPolicy.FreshnessSeconds) : AdministratorPolicy.FreshnessSeconds;
        return Math.Min(Math.Min(now + CodeSeconds, session.ExpiresAt), Math.Min(session.PasswordVerifiedAt + age, session.FactorVerifiedAt + age));
    }

    /// <summary>Bounds access and identity tokens by the source absolute expiry without replacing their original authentication times.</summary>
    internal static long TokenExpiry(AdministratorSession session, long now) => Math.Min(now + TokenSeconds, session.ExpiresAt);
}
