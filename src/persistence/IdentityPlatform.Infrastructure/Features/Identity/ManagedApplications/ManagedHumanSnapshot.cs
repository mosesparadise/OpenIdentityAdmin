using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Freezes all intended human proof and account/audit state before the final save and compares independent persisted reads afterward.</summary>
internal sealed class ManagedHumanSnapshot
{
    private readonly ManagedLoginInteraction row;
    private readonly string binding;
    private readonly CeremonyAccount? account;
    private readonly string accountBinding;
    private readonly SecurityAuditIntent? audit;
    private readonly string auditBinding;
    private readonly AdministratorInteraction? ceremony;
    private readonly string ceremonyBinding;

    /// <summary>Captures scalar digests before native persistence callbacks can mutate tracked or stored authority.</summary>
    internal ManagedHumanSnapshot(ManagedLoginInteraction row, CeremonyAccount? account, AdministratorInteraction? ceremony, SecurityAuditIntent? audit)
    {
        this.row = row;
        this.account = account;
        this.ceremony = ceremony;
        this.audit = audit;
        binding = FactorBinding.Digest(row);
        accountBinding = AccountBinding(account?.User, account?.Enrollment, account?.Authentication);
        auditBinding = FactorBinding.Digest(audit);
        ceremonyBinding = FactorBinding.Digest(ceremony);
    }

    /// <summary>Rejects any last-save mutation or expired original human proof; no public aggregate revision is refreshed.</summary>
    internal async Task VerifyAsync(PlatformDbContext database, TimeProvider clock, CancellationToken token)
    {
        if (binding != FactorBinding.Digest(row) || ManagedLoginRules.Observe(clock, row.ObservedAt) >= row.ExpiresAt ||
            binding != FactorBinding.Digest(await database.Set<ManagedLoginInteraction>().AsNoTracking().SingleAsync(value => value.SelectorDigest == row.SelectorDigest, token)))
        {
            throw new FactorUnavailableException();
        }
        if (account is not null)
        {
            var user = await database.Users.AsNoTracking().SingleAsync(value => value.Id == account.User.Id, token);
            var enrollment = await database.Set<AdministratorEnrollment>().AsNoTracking().SingleAsync(value => value.UserId == user.Id, token);
            var authentication = await database.Set<AdministratorAuthentication>().AsNoTracking().SingleAsync(value => value.UserId == user.Id, token);
            if (accountBinding != AccountBinding(account.User, account.Enrollment, account.Authentication) || accountBinding != AccountBinding(user, enrollment, authentication))
            {
                throw new FactorUnavailableException();
            }
        }
        if (ceremony is not null && (ceremonyBinding != FactorBinding.Digest(ceremony) || ceremonyBinding != FactorBinding.Digest(
            await database.Set<AdministratorInteraction>().AsNoTracking().SingleAsync(value => value.Id == ceremony.Id, token))))
        {
            throw new FactorUnavailableException();
        }
        if (audit is not null && (auditBinding != FactorBinding.Digest(audit) || auditBinding != FactorBinding.Digest(
            await database.Set<SecurityAuditIntent>().AsNoTracking().SingleAsync(value => value.Id == audit.Id, token))))
        {
            throw new FactorUnavailableException();
        }
    }

    /// <summary>Captures current scalar account fields without serializing native EF navigation collections.</summary>
    private static string AccountBinding(PlatformUser? user, AdministratorEnrollment? enrollment, AdministratorAuthentication? authentication) =>
        FactorBinding.Digest(new
        {
            user,
            enrollment,
            authentication
        });
}
