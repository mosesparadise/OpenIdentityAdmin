using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;

namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Binds one successful native verification to its immutable command, original session and transaction.</summary>
internal sealed class GovernanceProof
{
    private readonly GovernanceVerification verification;
    private bool consumed;
    internal GovernanceRequest Request => verification.Request;
    internal string ActorId => verification.Actor.User.Id;

    /// <summary>Captures only an actual successful verification; callers cannot construct or relabel ceremony proofs.</summary>
    private GovernanceProof(GovernanceVerification verification) => this.verification = verification;

    /// <summary>Creates command authority only from a current accepted native verification with a reserved active code.</summary>
    internal static GovernanceProof Create(CeremonyTransaction transaction, GovernanceVerification verification, long now)
    {
        verification.Claim(transaction, true, now);
        return new GovernanceProof(verification);
    }

    /// <summary>Consumes original command authorization once immediately before its protected writes and full-budget reset.</summary>
    internal void Consume(CeremonyTransaction transaction, CeremonyAccount account, long now)
    {
        verification.RequireCurrent(transaction, Request, now);
        if (consumed || !ReferenceEquals(account, verification.Actor))
        {
            throw new FactorUnavailableException();
        }
        consumed = true;
    }

    /// <summary>Preserves ownership and time after intentional self-revocation without requiring revoked authority to remain active.</summary>
    internal void RequireConsumed(CeremonyTransaction transaction, GovernanceRequest request, long now)
    {
        transaction.RequireGovernance();
        verification.Actor.RequireCurrent(transaction);
        if (!consumed || !ReferenceEquals(Request, request) || now < verification.VerifiedAt ||
            now - verification.VerifiedAt >= AdministratorPolicy.FreshnessSeconds)
        {
            throw new FactorUnavailableException();
        }
        verification.Session.RequireTime(now);
    }
}
