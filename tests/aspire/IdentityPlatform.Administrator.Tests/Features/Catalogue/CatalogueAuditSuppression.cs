using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Observes already-persisted native scope and policy before suppressing only the catalogue audit SaveChanges.</summary>
internal sealed class CatalogueAuditSuppression : SaveChangesInterceptor
{
    private const string Name = "email";
    internal bool ObservedNativeAndPolicy
    {
        get; private set;
    }

    /// <summary>Proves the test reaches persisted uncommitted state rather than relying on a tracker-only or pre-write exception.</summary>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var database = eventData.Context!;
        if (database.ChangeTracker.Entries<SecurityAuditIntent>().Any(entry => entry.State == EntityState.Added && entry.Entity.TargetType == CatalogueAssertions.ScopeTarget))
        {
            ObservedNativeAndPolicy = await database.Set<OpenIddictEntityFrameworkCoreScope>().AsNoTracking().AnyAsync(row => row.Name == Name, cancellationToken) &&
                await database.Set<ScopePolicy>().AsNoTracking().AnyAsync(cancellationToken);
            return InterceptionResult<int>.SuppressWithResult(0);
        }
        return result;
    }
}
