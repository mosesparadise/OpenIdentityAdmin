using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Admin.Api.Features.Authentication;

/// <summary>Shares metadata-only current account predicates; these observations grant no write or publication authority.</summary>
internal static class ApiSourceObservation
{
    private const int FactorMultiplicity = 2;

    /// <summary>Uses shared current security predicates and checks only finite factor metadata, without decrypting or renewing authentication.</summary>
    internal static async Task<ApiSourceState?> ReadAsync(PlatformDbContext database, ApiIdentity identity, TimeProvider clock, CancellationToken token)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(row => row.Id == identity.Subject, token);
        var enrollment = await database.Set<AdministratorEnrollment>().AsNoTracking().SingleOrDefaultAsync(row => row.UserId == identity.Subject, token);
        var authentication = await database.Set<AdministratorAuthentication>().AsNoTracking().SingleOrDefaultAsync(row => row.UserId == identity.Subject, token);
        var session = await database.Set<AdministratorSession>().AsNoTracking().SingleOrDefaultAsync(row => row.PublicId == identity.Session, token);
        if (user is null || enrollment is null || authentication is null || session is null ||
            user.Id != identity.Subject || enrollment.UserId != identity.Subject || authentication.UserId != identity.Subject ||
            session.UserId != identity.Subject || session.PublicId != identity.Session ||
            !new CeremonyClock(clock).TryRead(authentication, out var now) ||
            !JourneySessionState.Allows(user, enrollment, authentication, session, Math.Max(now, authentication.ObservedAt ?? now)))
        {
            return null;
        }
        var factors = await database.Set<AdministratorFactor>().AsNoTracking()
            .Where(row => row.UserId == identity.Subject && row.Generation == enrollment.FactorGeneration)
            .Select(row => new { row.UserId, row.Generation, Present = row.ProtectedSeed != string.Empty })
            .Take(FactorMultiplicity).ToArrayAsync(token);
        return factors is [var factor] && factor.UserId == identity.Subject && factor.Generation == session.FactorGeneration && factor.Present
            ? new(Math.Min(session.ExpiresAt, session.LastActivityAt + AdministratorPolicy.IdleSeconds), session.FactorVerifiedAt,
                new(session.Generation, session.FactorGeneration, enrollment.Revision, session.SecurityStamp, session.PasswordVerifiedAt, session.CreatedAt)) : null;
    }
}

/// <summary>Retains source expiry and actual persisted MFA completion without renewing either.</summary>
internal sealed record ApiSourceState(long ExpiresAt, long FactorVerifiedAt, ApiSourceBinding Binding);

/// <summary>Freezes security-relevant source bindings while excluding technical lock revisions and renewable activity timestamps.</summary>
internal sealed record ApiSourceBinding(long Generation, long FactorGeneration, long EnrollmentRevision, string Stamp, long PasswordVerifiedAt, long CreatedAt);
