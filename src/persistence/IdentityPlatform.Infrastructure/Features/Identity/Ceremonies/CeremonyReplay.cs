using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Shares one protected, bounded accepted-code ledger across enrollment, login and sensitive ceremonies.</summary>
internal sealed class CeremonyReplay(PlatformDbContext database, UserManager<PlatformUser> users, FactorAccess access,
    CeremonyReplayLedger ledger, CeremonyClock clock, TimeProvider time, CeremonyBudget budget)
{
    /// <summary>Checks replay before native verification, then resamples policy time and reserves the actual destination generation.</summary>
    internal Task<CeremonyProof?> VerifyAsync(CeremonyTransaction transaction, CeremonyAccount account,
        CeremonyInteraction interaction, string code, bool staged) =>
        CeremonyProof.VerifyAsync(this, transaction, account, interaction, code, staged);

    /// <summary>Provides the private proof factory with a result only after native verification and protected reservation.</summary>
    internal async Task<(long Generation, long VerifiedAt, VerifiedStagedFactor? Staged)?> ReserveAsync(CeremonyTransaction transaction, CeremonyAccount account,
        CeremonyInteraction interaction, string code, bool staged)
    {
        transaction.RequireContext(database);
        account.RequireCurrent(transaction);
        ((AdministratorUserManager)users).RequireOperation(transaction.Token);
        if (!await budget.OpenAsync(transaction, account) || !CeremonyReplayLedger.IsCode(code) || !clock.Observe(account.Authentication, out var before) || !CeremonyInteraction.IsCurrent(interaction.Row, account, before))
        {
            return null;
        }
        interaction.RequireCurrent(transaction, account, before);
        var generation = staged ? interaction.Row.DestinationGeneration : account.Enrollment.FactorGeneration;
        if (generation <= 0 || (!staged && !account.IsActive))
        {
            return null;
        }
        if (!await ledger.CanReserveAsync(transaction, account, generation, code))
        {
            return null;
        }
        var proof = staged ? await VerifiedStagedFactor.VerifyAsync(database, access, users, time, interaction.Row.Id, code, transaction.Token) : null;
        if (staged ? proof is null : !await ledger.VerifyActiveAsync(transaction, account, code))
        {
            return null;
        }
        if (!clock.Observe(account.Authentication, out var now) || !CeremonyInteraction.IsCurrent(interaction.Row, account, now))
        {
            return null;
        }
        interaction.RequireCurrent(transaction, account, now);
        await ledger.ReserveAsync(transaction, account, generation, code, now);
        return (generation, now, proof);
    }

    /// <summary>Prunes only safely expired metadata under the account lock and advances the durable clock fence.</summary>
    internal Task<int> PruneAsync(CeremonyTransaction transaction, CeremonyAccount account) => ledger.PruneAsync(transaction, account);
}
