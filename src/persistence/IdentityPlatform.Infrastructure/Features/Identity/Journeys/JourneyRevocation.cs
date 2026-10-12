using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Invalidates all earlier local authority within the owning credential mutation transaction.</summary>
internal sealed class JourneyRevocation(PlatformDbContext database)
{
    /// <summary>Preserves replay evidence and permanent bootstrap ownership while ending old sessions and interactions.</summary>
    internal async Task RevokeAsync(CeremonyTransaction transaction, CeremonyAccount account)
    {
        transaction.RequireContext(database);
        account.RequireCurrent(transaction);
        var userId = account.User.Id;
        await database.Set<AdministratorSession>().Where(row => row.UserId == userId && !row.Revoked)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revoked, true)
                .SetProperty(row => row.Revision, row => row.Revision + 1), transaction.Token);
        await database.Set<LocalIdentitySession>().Where(row => row.UserId == userId && !row.Revoked)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revoked, true), transaction.Token);
        await database.Set<AdministratorInteraction>().Where(row => row.UserId == userId && row.State == AdministratorPolicy.Pending)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.State, AdministratorPolicy.Consumed)
                .SetProperty(row => row.StagedSeed, (string?)null)
                .SetProperty(row => row.Revision, row => row.Revision + 1), transaction.Token);
    }
}
