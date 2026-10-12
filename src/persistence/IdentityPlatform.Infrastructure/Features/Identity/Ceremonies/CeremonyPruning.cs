using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Prunes replay metadata without decrypting secrets or weakening the durable clock fence.</summary>
internal static class CeremonyPruning
{
    /// <summary>Deletes at most the remaining category budget under the owning account lock; unsafe rollback retains all evidence.</summary>
    internal static async Task<int> ReplayAsync(PlatformDbContext database, CeremonyTransaction transaction,
        AdministratorAuthentication state, CeremonyClock clock, int limit)
    {
        transaction.RequireContext(database);
        var entry = database.Entry(state);
        if (entry.State is not (EntityState.Unchanged or EntityState.Modified) ||
            !transaction.Plan.Users.Contains(state.UserId, StringComparer.Ordinal) || limit is < 1 or > AdministratorPolicy.CleanupRows)
        {
            throw new FactorUnavailableException();
        }
        if (!clock.Observe(state, out var now))
        {
            return 0;
        }
        var before = now - AdministratorPolicy.ReplaySeconds;
        var ids = await database.Set<AdministratorReplay>().Where(row => row.UserId == state.UserId && row.AcceptedAt < before && row.ExpiresAt < now)
            .OrderBy(row => row.Id).Take(limit).Select(row => row.Id).ToArrayAsync(transaction.Token);
        return await database.Set<AdministratorReplay>().Where(row => ids.Contains(row.Id)).ExecuteDeleteAsync(transaction.Token);
    }
}
