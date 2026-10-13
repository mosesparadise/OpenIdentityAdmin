using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Contains untrusted cookie identifiers that must be reread under account serialization.</summary>
internal sealed record JourneySessionCandidate(string UserId, string SessionId);

/// <summary>Represents a current native full session validated within exactly one sensitive-operation transaction.</summary>
internal sealed class JourneySessionProof
{
    private readonly CeremonyTransaction owner;
    private readonly PlatformDbContext database;
    private readonly string userId;
    private readonly long createdAt;
    private readonly long expiresAt;
    private readonly long lastActivityAt;
    private readonly AdministratorSession session;
    private readonly string fingerprint;
    internal string PublicId => session.PublicId;

    /// <summary>Captures only freshly loaded, natively checked stored session authority.</summary>
    private JourneySessionProof(PlatformDbContext database, CeremonyTransaction owner, AdministratorSession session)
    {
        this.database = database;
        userId = session.UserId;
        createdAt = session.CreatedAt;
        expiresAt = session.ExpiresAt;
        lastActivityAt = session.LastActivityAt;
        this.owner = owner;
        this.session = session;
        fingerprint = FactorBinding.Digest(session);
    }

    /// <summary>Rejects foreign or stale cookies and validates protected active material without nesting a transaction.</summary>
    internal static async Task<JourneySessionProof?> ReadAsync(PlatformDbContext database, UserManager<PlatformUser> users,
        CeremonyClock clock, CeremonyTransaction transaction, CeremonyAccount account, JourneySessionCandidate? candidate)
    {
        transaction.RequireContext(database);
        account.RequireCurrent(transaction);
        ((AdministratorUserManager)users).RequireOperation(database, transaction.Token);
        if (candidate is null || candidate.UserId != account.User.Id || !CeremonySecrets.IsIdentifier(candidate.SessionId))
        {
            return null;
        }
        var session = await database.Set<AdministratorSession>().SingleOrDefaultAsync(row => row.PublicId == candidate.SessionId, transaction.Token);
        if (session is null || session.PublicId != candidate.SessionId || session.UserId != account.User.Id ||
            !clock.Observe(account.Authentication, out var before) ||
            !JourneySessionState.Allows(account.User, account.Enrollment, account.Authentication, session, before) ||
            string.IsNullOrEmpty(await users.GetAuthenticatorKeyAsync(account.User)) ||
            !clock.Observe(account.Authentication, out var now) ||
            !JourneySessionState.Allows(account.User, account.Enrollment, account.Authentication, session, now))
        {
            return null;
        }
        return new JourneySessionProof(database, transaction, session);
    }

    /// <summary>Rechecks the original live authority before credential writes invalidate its stamp and generation.</summary>
    internal void RequireCurrent(CeremonyTransaction transaction, CeremonyAccount account, long now)
    {
        account.RequireCurrent(transaction);
        transaction.RequireContext(database);
        if (!ReferenceEquals(owner, transaction) || userId != account.User.Id || session.UserId != userId ||
            database.Entry(session).State != EntityState.Unchanged || FactorBinding.Digest(session) != fingerprint ||
            !JourneySessionState.Allows(account.User, account.Enrollment, account.Authentication, session, now))
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Preserves the initiating session's absolute and idle limits even after intentional credential invalidation.</summary>
    internal void RequireTime(long now)
    {
        if (now < createdAt || now >= expiresAt || now - lastActivityAt >= AdministratorPolicy.IdleSeconds)
        {
            throw new FactorUnavailableException();
        }
    }
}
