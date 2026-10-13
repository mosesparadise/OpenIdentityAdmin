using System.Security.Cryptography;
using System.Text;
using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Shares bounded native active-factor verification and protected replay storage without issuing authorization.</summary>
internal sealed class CeremonyReplayLedger(PlatformDbContext database, UserManager<PlatformUser> users,
    FactorAccess access, FactorProtection protection, CeremonyClock clock)
{
    /// <summary>Rejects malformed, duplicate or over-capacity input while retaining corrupt-entry failure semantics.</summary>
    internal async Task<bool> CanReserveAsync(CeremonyTransaction transaction, CeremonyAccount account, long generation, string code)
        => await InspectAsync(transaction, account, generation, code) == CeremonyReplayAvailability.Ready;

    /// <summary>Distinguishes capacity exhaustion from a malformed or previously accepted code before native verification.</summary>
    internal async Task<CeremonyReplayAvailability> InspectAsync(CeremonyTransaction transaction, CeremonyAccount account, long generation, string code)
    {
        transaction.RequireContext(database);
        account.RequireCurrent(transaction);
        ((AdministratorUserManager)users).RequireOperation(database, transaction.Token);
        if (generation <= 0 || !IsCode(code))
        {
            return CeremonyReplayAvailability.Invalid;
        }
        await PruneAsync(transaction, account);
        var entries = await database.Set<AdministratorReplay>().Where(row => row.UserId == account.User.Id && row.Generation == generation)
            .OrderBy(row => row.Id).Take(AdministratorPolicy.ReplayEntries + 1).ToListAsync(transaction.Token);
        if (entries.Count >= AdministratorPolicy.ReplayEntries)
        {
            CeremonyMetrics.Record(CeremonyOutcome.ReplayDenied);
            return CeremonyReplayAvailability.Capacity;
        }
        if (IsReplay(entries, account.User.Id, generation, code))
        {
            CeremonyMetrics.Record(CeremonyOutcome.ReplayDenied);
            return CeremonyReplayAvailability.Replayed;
        }
        return CeremonyReplayAvailability.Ready;
    }

    /// <summary>Uses only the native active authenticator; ambient staged or recovery selection is rejected.</summary>
    internal Task<bool> VerifyActiveAsync(CeremonyTransaction transaction, CeremonyAccount account, string code)
    {
        transaction.RequireContext(database);
        account.RequireCurrent(transaction);
        access.RequireActive();
        ((AdministratorUserManager)users).RequireOperation(database, transaction.Token);
        return users.VerifyTwoFactorTokenAsync(account.User, TokenOptions.DefaultAuthenticatorProvider, code);
    }

    /// <summary>Stores a protected reservation only after its typed caller rechecks current proof bindings and time.</summary>
    internal async Task ReserveAsync(CeremonyTransaction transaction, CeremonyAccount account, long generation, string code, long now)
    {
        transaction.RequireContext(database);
        account.RequireCurrent(transaction);
        database.Add(new AdministratorReplay
        {
            UserId = account.User.Id,
            Generation = generation,
            AcceptedAt = now,
            ExpiresAt = checked(now + AdministratorPolicy.ReplaySeconds),
            ProtectedCode = protection.Protect(code, account.User.Id, generation, FactorProtection.Replay)
        });
        await database.SaveChangesAsync(transaction.Token);
        CeremonyMetrics.Record(CeremonyOutcome.Verified);
    }

    /// <summary>Prunes only safely expired metadata under the original account lock and clock fence.</summary>
    internal Task<int> PruneAsync(CeremonyTransaction transaction, CeremonyAccount account)
    {
        transaction.RequireContext(database);
        account.RequireCurrent(transaction);
        ((AdministratorUserManager)users).RequireOperation(database, transaction.Token);
        return CeremonyPruning.ReplayAsync(database, transaction, account.Authentication, clock, AdministratorPolicy.CleanupRows);
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
    internal static bool IsCode(string code) => code.Length == AdministratorPolicy.CodeDigits && code.All(character => character is >= '0' and <= '9');
}

/// <summary>Separates bounded ledger availability from actual incorrect or replayed credential input.</summary>
internal enum CeremonyReplayAvailability
{
    Ready, Invalid, Replayed, Capacity
}
