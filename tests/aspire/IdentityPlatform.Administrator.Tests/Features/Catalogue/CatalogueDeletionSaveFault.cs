using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Injects a finite save failure after actual target removal, proving rollback of native and product effects.</summary>
internal sealed class CatalogueDeletionSaveFault(bool substitute) : SaveChangesInterceptor
{
    private const string Deleted = "CatalogueDeleted";
    internal bool Observed
    {
        get; private set;
    }

    /// <summary>Observes independent database absence, then suppresses terminal evidence or alters original approval before the terminal save.</summary>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var database = eventData.Context!;
        var terminal = database.ChangeTracker.Entries<SecurityAuditIntent>().SingleOrDefault(entry => entry.State == EntityState.Added && entry.Entity.Event == Deleted);
        if (terminal is null)
        {
            return result;
        }
        var target = terminal.Entity.TargetId;
        Observed = !await database.Set<OpenIddictEntityFrameworkCoreScope>().AsNoTracking().AnyAsync(row => row.Id == target, cancellationToken) &&
            !await database.Set<ScopePolicy>().AsNoTracking().AnyAsync(row => row.ScopeId == target, cancellationToken) &&
            !await database.Set<ApiAudience>().AsNoTracking().AnyAsync(row => row.Id == target, cancellationToken);
        if (substitute)
        {
            database.ChangeTracker.Entries<CatalogueDeletion>().Single().Entity.ApprovalUntil--;
            return result;
        }
        return InterceptionResult<int>.SuppressWithResult(0);
    }
}
