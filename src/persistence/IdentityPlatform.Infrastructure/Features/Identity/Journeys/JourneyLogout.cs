using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Revokes exactly one currently authorized stored session before a browser may clear its named cookie.</summary>
internal sealed class JourneyLogout(PlatformDbContext database, NativeOperation operation, UserManager<PlatformUser> users, CeremonyClock clock)
{
    /// <summary>Rereads current authority under serialization and commits the exact-session revocation together with its audit.</summary>
    internal async Task<bool> RevokeAsync(JourneySessionCandidate? candidate, CancellationToken token)
    {
        if (candidate is null || string.IsNullOrEmpty(candidate.UserId) || candidate.UserId.Length > FactorSchema.UserLength ||
            !CeremonySecrets.IsIdentifier(candidate.SessionId))
        {
            return false;
        }
        var plan = new CeremonyLockPlan(false, false, [], [candidate.UserId]);
        await using var transaction = await CeremonyTransaction.OpenAsync(database, operation, plan, token);
        var account = await CeremonyAccount.LoadAsync(database, transaction, candidate.UserId);
        var authority = await JourneySessionProof.ReadAsync(database, users, clock, transaction, account, candidate);
        if (authority is null)
        {
            return false;
        }
        authority.RequireCurrent(transaction, account, clock.Now);
        var session = await database.Set<AdministratorSession>().SingleAsync(row => row.PublicId == candidate.SessionId, transaction.Token);
        if (!clock.Observe(account.Authentication, out var now))
        {
            throw new FactorUnavailableException();
        }
        authority.RequireCurrent(transaction, account, now);
        session.Revoked = true;
        session.Revision = checked(session.Revision + 1);
        await transaction.CommitAsync(CeremonyEvent.Logout, true, account.User.Id, now);
        return true;
    }
}
