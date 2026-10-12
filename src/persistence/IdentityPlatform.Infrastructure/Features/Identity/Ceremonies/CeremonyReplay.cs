using System.Security.Cryptography;
using System.Text;
using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Shares one protected, bounded accepted-code ledger across enrollment, login and sensitive ceremonies.</summary>
internal sealed class CeremonyReplay(PlatformDbContext database, UserManager<PlatformUser> users, FactorAccess access,
    FactorProtection protection, CeremonyClock clock, TimeProvider time, CeremonyBudget budget)
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
        if (!await budget.OpenAsync(transaction, account) || !IsCode(code) || !clock.Observe(account.Authentication, out var before) || !CeremonyInteraction.IsCurrent(interaction.Row, account, before))
        {
            return null;
        }
        interaction.RequireCurrent(transaction, account, before);
        var generation = staged ? interaction.Row.DestinationGeneration : account.Enrollment.FactorGeneration;
        if (generation <= 0 || (!staged && !account.IsActive))
        {
            return null;
        }
        await PruneAsync(transaction, account);
        var entries = await database.Set<AdministratorReplay>().Where(row => row.UserId == account.User.Id && row.Generation == generation)
            .OrderBy(row => row.Id).Take(AdministratorPolicy.ReplayEntries + 1).ToListAsync(transaction.Token);
        if (entries.Count >= AdministratorPolicy.ReplayEntries || IsReplay(entries, account.User.Id, generation, code))
        {
            CeremonyMetrics.Record(CeremonyOutcome.ReplayDenied);
            return null;
        }
        var proof = staged ? await VerifiedStagedFactor.VerifyAsync(database, access, users, time, interaction.Row.Id, code, transaction.Token) : null;
        if (staged ? proof is null : !await users.VerifyTwoFactorTokenAsync(account.User, TokenOptions.DefaultAuthenticatorProvider, code))
        {
            return null;
        }
        if (!clock.Observe(account.Authentication, out var now) || !CeremonyInteraction.IsCurrent(interaction.Row, account, now))
        {
            return null;
        }
        interaction.RequireCurrent(transaction, account, now);
        database.Add(new AdministratorReplay
        {
            UserId = account.User.Id,
            Generation = generation,
            AcceptedAt = now,
            ExpiresAt = now + AdministratorPolicy.ReplaySeconds,
            ProtectedCode = protection.Protect(code, account.User.Id, generation, FactorProtection.Replay)
        });
        await database.SaveChangesAsync(transaction.Token);
        CeremonyMetrics.Record(CeremonyOutcome.Verified);
        return (generation, now, proof);
    }

    /// <summary>Prunes only safely expired metadata under the account lock and advances the durable clock fence.</summary>
    internal async Task<int> PruneAsync(CeremonyTransaction transaction, CeremonyAccount account)
    {
        transaction.RequireContext(database);
        account.RequireCurrent(transaction);
        ((AdministratorUserManager)users).RequireOperation(transaction.Token);
        return await CeremonyPruning.ReplayAsync(database, transaction, account.Authentication, clock, AdministratorPolicy.CleanupRows);
    }

    /// <summary>Compares every bounded protected code; any corrupt entry fails the decision rather than being ignored.</summary>
    private bool IsReplay(IEnumerable<AdministratorReplay> entries, string userId, long generation, string code)
    {
        var matched = false;
        var submitted = Encoding.ASCII.GetBytes(code);
        foreach (var entry in entries)
        {
            if (entry.UserId != userId || entry.Generation != generation)
            {
                throw new FactorUnavailableException();
            }
            var accepted = protection.Unprotect(entry.ProtectedCode, userId, generation, FactorProtection.Replay);
            if (!IsCode(accepted))
            {
                throw new FactorUnavailableException();
            }
            var bytes = Encoding.ASCII.GetBytes(accepted);
            matched |= CryptographicOperations.FixedTimeEquals(submitted, bytes);
            CryptographicOperations.ZeroMemory(bytes);
        }
        CryptographicOperations.ZeroMemory(submitted);
        return matched;
    }

    /// <summary>Requires exactly six ASCII digits without trimming or integer normalization.</summary>
    private static bool IsCode(string code) => code.Length == AdministratorPolicy.CodeDigits && code.All(character => character is >= '0' and <= '9');
}
