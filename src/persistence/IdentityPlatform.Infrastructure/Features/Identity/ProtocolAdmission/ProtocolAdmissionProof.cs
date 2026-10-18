using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.ProtocolAdmission;

/// <summary>Retains current source authority inside one locked issuance epoch; it cannot be reused after commit or cleanup.</summary>
internal sealed class ProtocolAdmissionProof
{
    private readonly ProtocolAdmissionOwnership ownership;
    private readonly PlatformDbContext database;
    private readonly PlatformUser user;
    private readonly AdministratorEnrollment enrollment;
    private readonly AdministratorAuthentication authentication;
    private readonly AdministratorSession session;
    private readonly long? maximumAge;
    private readonly string binding;

    /// <summary>Captures only fresh state whose current factor and persisted permission were verified under the same locks.</summary>
    private ProtocolAdmissionProof(ProtocolAdmissionOwnership ownership, PlatformDbContext database, PlatformUser user,
        AdministratorEnrollment enrollment, AdministratorAuthentication authentication, AdministratorSession session, long? maximumAge)
    {
        this.ownership = ownership;
        this.database = database;
        this.user = user;
        this.enrollment = enrollment;
        this.authentication = authentication;
        this.session = session;
        this.maximumAge = maximumAge;
        binding = Binding();
    }

    /// <summary>Rechecks the original state and strict time immediately before native issuance commit without refreshing proof.</summary>
    internal ValueTask<bool> IsCurrentAsync(CeremonyClock clock, CancellationToken token) =>
        ownership.RunAsync(database, cancellation => new ValueTask<bool>(IsCurrent(clock, cancellation, out _)), token);

    /// <summary>Checks live state only while the original owner's execution lease excludes competing operations.</summary>
    private bool IsCurrent(CeremonyClock clock, CancellationToken token, out long now)
    {
        ownership.RequireExecution(database, token);
        if (binding != Binding() || database.Entry(user).State == EntityState.Detached ||
            database.Entry(enrollment).State == EntityState.Detached || database.Entry(authentication).State == EntityState.Detached ||
            database.Entry(session).State == EntityState.Detached)
        {
            throw new FactorUnavailableException();
        }
        if (!clock.TryRead(authentication, out var sample))
        {
            now = sample;
            return false;
        }
        now = Math.Max(sample, authentication.ObservedAt!.Value);
        return JourneySessionState.Allows(user, enrollment, authentication, session, now) && ProtocolAdmissionTime.Allows(session, now, maximumAge);
    }

    /// <summary>Derives bounded claim inputs from one checked sample; callers cannot supply a time or read unchecked authority fields.</summary>
    internal ValueTask<ProtocolAdmissionClaims?> DescribeAsync(CeremonyClock clock, CancellationToken token) =>
        ownership.RunAsync(database, cancellation => ValueTask.FromResult<ProtocolAdmissionClaims?>(IsCurrent(clock, cancellation, out var now)
            ? new(user.Id, session.PublicId, session.PasswordVerifiedAt, session.FactorVerifiedAt, session.ExpiresAt,
                ProtocolAdmissionTime.CodeExpiry(session, now, maximumAge), ProtocolAdmissionTime.TokenExpiry(session, now)) : null), token);

    /// <summary>Detects in-process replacement of protected source fields without retaining printable secret-bearing state.</summary>
    private string Binding() => FactorBinding.Digest(new { user, enrollment, authentication, session });

    /// <summary>Distinguishes missing or stale authentication from a fully authenticated account with no current approved permission.</summary>
    internal static ValueTask<ProtocolAdmissionResult> ReadAsync(PlatformDbContext database, ProtocolAdmissionOwnership ownership,
        string sessionId, long? maximumAge, UserManager<PlatformUser> users, NativeOperation operation, CeremonyClock clock, CancellationToken token) =>
        ownership.RunAsync(database, cancellation => ReadCoreAsync(database, ownership, sessionId, maximumAge, users, operation, clock, cancellation), token);

    /// <summary>Performs every native read and factor check under the original phase's linked execution token.</summary>
    private static async ValueTask<ProtocolAdmissionResult> ReadCoreAsync(PlatformDbContext database, ProtocolAdmissionOwnership ownership,
        string sessionId, long? maximumAge, UserManager<PlatformUser> users, NativeOperation operation, CeremonyClock clock, CancellationToken token)
    {
        ownership.RequireExecution(database, token);
        if (!CeremonySecrets.IsIdentifier(sessionId) || maximumAge < 0)
        {
            return new(ProtocolAdmissionOutcome.LoginRequired, null);
        }
        using var native = operation.Enter(token);
        ((AdministratorUserManager)users).RequireOperation(database, token);
        var user = await database.Users.SingleOrDefaultAsync(row => row.Id == ownership.UserId, token);
        var enrollment = await database.Set<AdministratorEnrollment>().SingleOrDefaultAsync(row => row.UserId == ownership.UserId, token);
        var authentication = await database.Set<AdministratorAuthentication>().SingleOrDefaultAsync(row => row.UserId == ownership.UserId, token);
        var session = await database.Set<AdministratorSession>().SingleOrDefaultAsync(row => row.PublicId == sessionId, token);
        if (user is null || enrollment is null || authentication is null || session is null ||
            user.Id != ownership.UserId || enrollment.UserId != ownership.UserId || authentication.UserId != ownership.UserId ||
            session.UserId != ownership.UserId || session.PublicId != sessionId ||
            !Allows(user, enrollment, authentication, session, clock, maximumAge) || string.IsNullOrEmpty(await users.GetAuthenticatorKeyAsync(user)) ||
            !Allows(user, enrollment, authentication, session, clock, maximumAge))
        {
            return new(ProtocolAdmissionOutcome.LoginRequired, null);
        }
        var grants = await new GovernanceGrants(database).ReadProtocolAsync(ownership, token);
        if (grants.Permissions(ownership.UserId).Count == 0)
        {
            return new(ProtocolAdmissionOutcome.AccessDenied, null);
        }
        var proof = new ProtocolAdmissionProof(ownership, database, user, enrollment, authentication, session, maximumAge);
        return proof.IsCurrent(clock, token, out _) ? new(ProtocolAdmissionOutcome.Allowed, proof) : new(ProtocolAdmissionOutcome.LoginRequired, null);
    }

    /// <summary>Combines the existing full-session policy with strict original-proof freshness at a nondecreasing account clock.</summary>
    private static bool Allows(PlatformUser user, AdministratorEnrollment enrollment, AdministratorAuthentication authentication,
        AdministratorSession session, CeremonyClock clock, long? maximumAge)
    {
        if (!clock.Observe(authentication, out var sample))
        {
            return false;
        }
        var now = Math.Max(sample, authentication.ObservedAt!.Value);
        return JourneySessionState.Allows(user, enrollment, authentication, session, now) && ProtocolAdmissionTime.Allows(session, now, maximumAge);
    }
}
