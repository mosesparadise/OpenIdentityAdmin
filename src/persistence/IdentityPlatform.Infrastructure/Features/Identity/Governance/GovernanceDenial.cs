using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Authorizes only a failed native attempt and fixed denial audit after verification side effects were rolled back.</summary>
internal sealed class GovernanceDenial
{
    private readonly GovernanceVerification verification;
    private readonly string binding;
    private readonly long recordedAt;
    private bool consumed;

    /// <summary>Captures the exact policy-applied actor delta after the private factory completes its checks.</summary>
    private GovernanceDenial(GovernanceVerification verification, long recordedAt)
    {
        this.verification = verification;
        this.recordedAt = recordedAt;
        binding = Binding(verification.Actor);
    }

    /// <summary>Records a genuine failed credential attempt; unrelated policy and provider errors cannot mint this capability.</summary>
    internal static async Task<GovernanceDenial> RecordAsync(PlatformDbContext database, CeremonyTransaction transaction,
        GovernanceVerification verification, CeremonyBudget budget, CeremonyClock clock)
    {
        transaction.RequireContext(database);
        verification.Claim(transaction, false, clock.Now);
        var before = verification.Actor.Authentication.Failures;
        var expired = verification.Actor.Authentication.LockoutUntil is not null;
        await budget.FailAsync(transaction, verification.Actor);
        var expected = expired ? 1 : checked(before + 1);
        if (verification.Actor.Authentication.Failures != expected ||
            !clock.Observe(verification.Actor.Authentication, out var now))
        {
            throw new FactorUnavailableException();
        }
        verification.Session.RequireTime(now);
        return new GovernanceDenial(verification, now);
    }

    /// <summary>Consumes the same actor-only delta once; intentional fifth-attempt lockout uses time-only session validation.</summary>
    internal async Task WriteAsync(PlatformDbContext database, CeremonyTransaction transaction, long now)
    {
        transaction.RequireContext(database);
        transaction.RequireGovernance();
        verification.Actor.RequireCurrent(transaction);
        if (consumed || binding != Binding(verification.Actor) || now < recordedAt ||
            now - verification.VerifiedAt >= AdministratorPolicy.FreshnessSeconds)
        {
            throw new FactorUnavailableException();
        }
        verification.Session.RequireTime(now);
        var revision = await database.Set<AdministratorGovernance>().AsNoTracking()
            .Where(row => row.Id == GovernanceLimits.Singleton).Select(row => row.AuthorityRevision).SingleAsync(transaction.Token);
        if (revision != verification.Request.Versions.AuthorityRevision || database.ChangeTracker.Entries().Any(entry =>
            entry.State is EntityState.Added or EntityState.Deleted || entry.State == EntityState.Modified &&
            !ReferenceEquals(entry.Entity, verification.Actor.User) && !ReferenceEquals(entry.Entity, verification.Actor.Authentication)))
        {
            throw new FactorUnavailableException();
        }
        consumed = true;
        GovernanceAudit.Add(database, verification.Request, verification.Actor.User.Id, false, now);
    }

    /// <summary>Checks the exact post-failure scalar state without retaining plaintext credential snapshots.</summary>
    private static string Binding(CeremonyAccount account) => FactorBinding.Digest(new { account.User, account.Enrollment, account.Authentication });

    /// <summary>Preserves original session time after the fixed denial audit was persisted and before native commit.</summary>
    internal void RequireFinal(CeremonyTransaction transaction, long now)
    {
        verification.Actor.RequireCurrent(transaction);
        if (!consumed || now < recordedAt || now - verification.VerifiedAt >= AdministratorPolicy.FreshnessSeconds)
        {
            throw new FactorUnavailableException();
        }
        verification.Session.RequireTime(now);
    }
}
