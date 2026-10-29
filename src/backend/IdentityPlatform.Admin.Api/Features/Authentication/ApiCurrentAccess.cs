using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Admin.Api.Features.Authentication;

/// <summary>Reads current account/session and persisted permission under rollback-only ownership; no factor keys or clock writes are used.</summary>
internal static class ApiCurrentAccess
{
    private const string ReadPermission = "applications.read";
    private const int FactorMultiplicity = 2;

    /// <summary>Distinguishes current authentication denial from an otherwise eligible account lacking its persisted grant.</summary>
    internal static async Task<(int Status, long SourceExpiry)> ReadAsync(IServiceProvider services, PlatformDbContext database,
        CeremonyTransaction transaction, ApiIdentity identity, string callback, TimeProvider clock)
    {
        transaction.RequireApiRead(database, identity.Subject);
        var state = await SourceAsync(database, transaction, identity, clock);
        if (state is null || !await ApiReadProfile.AllowsAsync(services, database, transaction, identity, callback, clock.GetUtcNow().ToUnixTimeSeconds()))
        {
            return (StatusCodes.Status401Unauthorized, 0);
        }
        var grants = await new GovernanceGrants(database).ReadApiAsync(transaction, identity.Subject);
        transaction.RequireApiRead(database, identity.Subject);
        return (grants.Permissions(identity.Subject).Contains(ReadPermission) ? StatusCodes.Status200OK : StatusCodes.Status403Forbidden, state.Value);
    }

    /// <summary>Uses shared current security predicates and checks only finite factor metadata, without decrypting or renewing authentication.</summary>
    private static async Task<long?> SourceAsync(PlatformDbContext database, CeremonyTransaction transaction, ApiIdentity identity, TimeProvider clock)
    {
        var token = transaction.Token;
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
            ? Math.Min(session.ExpiresAt, session.LastActivityAt + AdministratorPolicy.IdleSeconds) : null;
    }
}
