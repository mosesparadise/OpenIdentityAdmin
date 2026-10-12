using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Allocates durable factor destinations without reusing committed abandoned or retired generations.</summary>
internal sealed class CeremonyGenerations(PlatformDbContext database)
{
    /// <summary>Checks persisted continuity under the account lock and advances the durable counter without committing independently.</summary>
    internal async Task<long> AllocateAsync(CeremonyTransaction transaction, CeremonyAccount account)
    {
        transaction.RequireContext(database);
        account.RequireCurrent(transaction);
        var userId = account.User.Id;
        var last = account.Authentication.LastAllocatedFactorGeneration;
        var factor = await database.Set<AdministratorFactor>().Where(row => row.UserId == userId)
            .MaxAsync(row => (long?)row.Generation, transaction.Token) ?? 0;
        var staged = await database.Set<AdministratorInteraction>().Where(row => row.UserId == userId)
            .MaxAsync(row => (long?)row.DestinationGeneration, transaction.Token) ?? 0;
        var replay = await database.Set<AdministratorReplay>().Where(row => row.UserId == userId)
            .MaxAsync(row => (long?)row.Generation, transaction.Token) ?? 0;
        if (last < 0 || last < account.Enrollment.FactorGeneration || last < factor || last < staged || last < replay)
        {
            throw new FactorUnavailableException();
        }
        account.Authentication.LastAllocatedFactorGeneration = checked(last + 1);
        return account.Authentication.LastAllocatedFactorGeneration;
    }
}
