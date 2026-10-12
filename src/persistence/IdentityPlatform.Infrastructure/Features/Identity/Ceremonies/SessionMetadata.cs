using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Defines the complete write allowance for validation of existing session authority.</summary>
internal static class SessionMetadata
{
    /// <summary>Rejects added/deleted entities, unrelated modifications, backwards activity and fabricated revision jumps.</summary>
    internal static void Require(PlatformDbContext database)
    {
        database.ChangeTracker.DetectChanges();
        foreach (var entry in database.ChangeTracker.Entries().Where(entry => entry.State != EntityState.Unchanged))
        {
            if (entry.State != EntityState.Modified)
            {
                throw new FactorUnavailableException();
            }
            foreach (var property in entry.Properties.Where(property => property.IsModified))
            {
                var allowed = entry.Entity switch
                {
                    AdministratorSession => property.Metadata.Name is nameof(AdministratorSession.LastActivityAt) or nameof(AdministratorSession.Revision),
                    AdministratorAuthentication => property.Metadata.Name == nameof(AdministratorAuthentication.ObservedAt),
                    _ => false
                };
                if (!allowed || property.CurrentValue is not long current ||
                    (property.OriginalValue is long original && (current < original ||
                    (property.Metadata.Name == nameof(AdministratorSession.Revision) && current != checked(original + 1)))))
                {
                    throw new FactorUnavailableException();
                }
            }
        }
    }
}
