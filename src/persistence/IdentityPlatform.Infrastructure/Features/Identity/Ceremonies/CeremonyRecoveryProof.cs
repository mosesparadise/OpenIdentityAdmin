using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Represents actual single-use native recovery redemption, never a TOTP or full-session proof.</summary>
internal sealed class CeremonyRecoveryProof
{
    private readonly CeremonyTransaction owner;
    private readonly CeremonyInteraction interaction;
    private readonly string binding;
    private readonly long recoveryGeneration;
    private bool consumed;
    internal long VerifiedAt
    {
        get;
    }

    /// <summary>Captures only the native manager's successful conditional claim and final freshness check.</summary>
    private CeremonyRecoveryProof(CeremonyTransaction owner, CeremonyAccount account, CeremonyInteraction interaction, long now)
    {
        this.owner = owner;
        this.interaction = interaction;
        binding = FactorBinding.Digest(interaction.Row);
        recoveryGeneration = account.Enrollment.RecoveryGeneration;
        VerifiedAt = now;
    }

    /// <summary>Claims one canonical verifier in the selected current generation and leaves all remaining verifiers intact.</summary>
    internal static async Task<CeremonyRecoveryProof?> RedeemAsync(PlatformDbContext database, UserManager<PlatformUser> users, FactorAccess access,
        CeremonyClock clock, CeremonyBudget budget, CeremonyTransaction transaction, CeremonyAccount account, CeremonyInteraction interaction, string code)
    {
        transaction.RequireContext(database);
        account.RequireCurrent(transaction);
        ((AdministratorUserManager)users).RequireOperation(transaction.Token);
        interaction.RequireCurrent(transaction, account, clock.Now);
        if (!transaction.Plan.Governance || interaction.Row.Purpose != AdministratorPolicy.Recovery || !RecoveryVerifier.IsCanonical(code) ||
            !account.User.IsEnabled || !account.User.TwoFactorEnabled || account.Enrollment.Suspended ||
            account.Enrollment.State is not (AdministratorPolicy.Active or AdministratorPolicy.RecoveryRequired) ||
            account.Enrollment.FactorGeneration <= 0 || account.Enrollment.RecoveryGeneration <= 0 ||
            !await budget.OpenAsync(transaction, account))
        {
            return null;
        }
        var verifier = RecoveryVerifier.Hash(account.User.Id, account.Enrollment.RecoveryGeneration, code);
        if (!await database.Set<AdministratorRecoveryCode>().AnyAsync(row => row.UserId == account.User.Id &&
            row.Generation == account.Enrollment.RecoveryGeneration && row.Verifier == verifier && row.RedeemedAt == null, transaction.Token))
        {
            return null;
        }
        using var selection = access.SelectRecovery(account.Enrollment, transaction.Token);
        if (!(await users.RedeemTwoFactorRecoveryCodeAsync(account.User, code)).Succeeded)
        {
            throw new FactorUnavailableException();
        }
        if (!clock.Observe(account.Authentication, out var now) || !CeremonyInteraction.IsCurrent(interaction.Row, account, now))
        {
            throw new FactorUnavailableException();
        }
        interaction.RequireCurrent(transaction, account, now);
        return new CeremonyRecoveryProof(transaction, account, interaction, now);
    }

    /// <summary>Consumes restricted recovery authorization once under its original account, generation and browser binding.</summary>
    internal void Consume(CeremonyTransaction transaction, CeremonyAccount account, long now)
    {
        interaction.RequireCurrent(transaction, account, now);
        if (consumed || !ReferenceEquals(owner, transaction) || binding != FactorBinding.Digest(interaction.Row) ||
            recoveryGeneration != account.Enrollment.RecoveryGeneration || now < VerifiedAt ||
            now - VerifiedAt >= AdministratorPolicy.FreshnessSeconds)
        {
            throw new FactorUnavailableException();
        }
        consumed = true;
    }
}
