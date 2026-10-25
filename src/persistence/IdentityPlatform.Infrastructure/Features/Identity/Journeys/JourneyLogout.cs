using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Revokes exactly one currently authorized stored session before a browser may clear its named cookie.</summary>
internal sealed class JourneyLogout(PlatformDbContext database, NativeOperation operation, UserManager<PlatformUser> users, CeremonyClock clock)
{
    /// <summary>Rereads current authority under serialization and commits the exact-session revocation together with its audit.</summary>
    internal async Task<bool> RevokeAsync(JourneySessionCandidate? candidate, CancellationToken token)
        => await RevokeCoreAsync(candidate, token) is not null;

    /// <summary>Mints a request-bound receipt only after actual audited revocation and successful transaction disposal.</summary>
    internal Task<Receipt?> RevokeForProtocolAsync(JourneySessionCandidate candidate, ProtocolStorageRequest request, JourneyLogoutDeadline expires, CancellationToken token)
        => Receipt.RevokeAsync(this, candidate, request, expires, token);

    /// <summary>Shares the same serialized single-session operation with ordinary account logout and native end-session.</summary>
    private async Task<JourneySessionProof?> RevokeCoreAsync(JourneySessionCandidate? candidate, CancellationToken token, JourneyLogoutDeadline? confirmation = null)
    {
        confirmation?.RequireCurrent(clock.Now);
        if (candidate is null || string.IsNullOrEmpty(candidate.UserId) || candidate.UserId.Length > FactorSchema.UserLength ||
            !CeremonySecrets.IsIdentifier(candidate.SessionId))
        {
            return null;
        }
        var plan = new CeremonyLockPlan(false, false, [], [candidate.UserId]);
        await using var transaction = await CeremonyTransaction.OpenAsync(database, operation, plan, token);
        var account = await CeremonyAccount.LoadAsync(database, transaction, candidate.UserId);
        var authority = await JourneySessionProof.ReadAsync(database, users, clock, transaction, account, candidate);
        if (authority is null)
        {
            return null;
        }
        authority.RequireCurrent(transaction, account, clock.Now);
        var session = await database.Set<AdministratorSession>().SingleAsync(row => row.PublicId == candidate.SessionId, transaction.Token);
        if (!clock.Observe(account.Authentication, out var now))
        {
            throw new FactorUnavailableException();
        }
        authority.RequireCurrent(transaction, account, now);
        confirmation?.RequireCurrent(now);
        session.Revoked = true;
        session.Revision = checked(session.Revision + 1);
        await transaction.CommitLogoutAsync(account.User.Id, now, authority, clock, confirmation);
        return authority;
    }

    /// <summary>Attests only this request's completed logout; construction is confined to the real revocation path.</summary>
    internal sealed class Receipt
    {
        private readonly ProtocolStorageRequest request;
        private readonly JourneySessionDeadline authority;
        private readonly JourneyLogoutDeadline expires;
        internal JourneySessionCandidate Candidate
        {
            get;
        }

        /// <summary>Executes the real private revocation before creating any publication capability.</summary>
        internal static async Task<Receipt?> RevokeAsync(JourneyLogout owner, JourneySessionCandidate candidate, ProtocolStorageRequest request, JourneyLogoutDeadline expires, CancellationToken token)
        {
            request.RequireCurrent();
            var authority = await owner.RevokeCoreAsync(candidate, token, expires);
            request.RequireCurrent();
            if (authority is null)
            {
                return null;
            }
            var receipt = new Receipt(request, candidate, authority.Deadline, expires);
            receipt.RequireCurrent(request);
            return receipt;
        }

        /// <summary>Retains the original source and confirmation deadlines without retaining a live database service.</summary>
        private Receipt(ProtocolStorageRequest request, JourneySessionCandidate candidate, JourneySessionDeadline authority, JourneyLogoutDeadline expires)
        {
            this.request = request;
            this.authority = authority;
            this.expires = expires;
            Candidate = candidate;
        }

        /// <summary>Rejects another request or publication after either original authority or confirmation expires.</summary>
        internal void RequireCurrent(ProtocolStorageRequest current)
        {
            current.RequireCurrent();
            var now = current.Clock.GetUtcNow().ToUnixTimeSeconds();
            if (!ReferenceEquals(current, request))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            authority.RequireCurrent(now);
            expires.RequireCurrent(now);
        }
    }
}
