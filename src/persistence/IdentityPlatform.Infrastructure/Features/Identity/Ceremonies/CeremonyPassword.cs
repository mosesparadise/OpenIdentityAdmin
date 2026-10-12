using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Represents actual native password verification for one locked account, transaction and requested purpose.</summary>
internal sealed class CeremonyPassword
{
    private readonly CeremonyTransaction transaction;
    private readonly string userId;
    private readonly string? stamp;
    private readonly string purpose;
    internal long VerifiedAt
    {
        get;
    }

    /// <summary>Captures the post-verification sample without granting a session or resetting the failure budget.</summary>
    private CeremonyPassword(CeremonyTransaction transaction, CeremonyAccount account, string purpose, long verifiedAt)
    {
        this.transaction = transaction;
        userId = account.User.Id;
        stamp = account.User.SecurityStamp;
        this.purpose = purpose;
        VerifiedAt = verifiedAt;
    }

    /// <summary>Uses native password checking under the durable budget; password-only success has no counter-reset side effect.</summary>
    internal static async Task<CeremonyPassword?> VerifyAsync(UserManager<PlatformUser> users, CeremonyBudget budget,
        CeremonyClock clock, CeremonyTransaction transaction, CeremonyAccount account, string purpose, string password)
    {
        account.RequireCurrent(transaction);
        ((AdministratorUserManager)users).RequireOperation(transaction.Token);
        if (!await budget.OpenAsync(transaction, account) || !await users.CheckPasswordAsync(account.User, password) ||
            !clock.Observe(account.Authentication, out var now))
        {
            return null;
        }
        return new CeremonyPassword(transaction, account, purpose, now);
    }

    /// <summary>Rejects invented, cross-purpose, stale or transplanted password freshness before interaction allocation.</summary>
    internal void RequireCurrent(CeremonyTransaction current, CeremonyAccount account, string requestedPurpose, long now)
    {
        account.RequireCurrent(current);
        if (!ReferenceEquals(current, transaction) || account.User.Id != userId || account.User.SecurityStamp != stamp ||
            requestedPurpose != purpose || now < VerifiedAt || now - VerifiedAt >= AdministratorPolicy.FreshnessSeconds)
        {
            throw new FactorUnavailableException();
        }
    }
}
