using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Catalogue;

/// <summary>Observes real uncommitted target absence immediately before the native terminal audit write, optionally cancelling the owning request.</summary>
internal sealed class CatalogueDeletionTerminalWitness(Action? cancel = null) : SaveChangesInterceptor
{
    private const string Deleted = "CatalogueDeleted";
    internal bool Observed
    {
        get; private set;
    }

    /// <summary>Never substitutes persistence: audit-permission failure still comes from the provider, and cancellation acts only after observed native deletion.</summary>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var database = eventData.Context!;
        var terminal = database.ChangeTracker.Entries<SecurityAuditIntent>().SingleOrDefault(entry => entry.State == EntityState.Added && entry.Entity.Event == Deleted);
        if (terminal is null)
        {
            return result;
        }
        var target = terminal.Entity.TargetId;
        Observed = !await database.Set<OpenIddictEntityFrameworkCoreScope>().AsNoTracking().AnyAsync(row => row.Id == target, cancellationToken) &&
            !await database.Set<ScopePolicy>().AsNoTracking().AnyAsync(row => row.ScopeId == target, cancellationToken);
        if (Observed)
        {
            cancel?.Invoke();
        }
        return result;
    }
}
