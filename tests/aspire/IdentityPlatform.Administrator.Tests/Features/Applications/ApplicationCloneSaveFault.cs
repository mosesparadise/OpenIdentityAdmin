using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Changes actual rows after the clone's audit save, leaving tracked observations stale so verification must read persisted truth.</summary>
internal sealed class ApplicationCloneSaveFault(ApplicationCloneDefect defect) : SaveChangesInterceptor
{
    private const string Substitution = "substituted-source";
    internal bool Armed
    {
        get; set;
    }
    internal bool Fired
    {
        get; private set;
    }
    internal string? ScopeId
    {
        get; set;
    }

    /// <summary>Injects one independently persisted provenance/source/catalogue corruption only after the genuine clone and its audit have been saved.</summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        var database = eventData.Context!;
        var detail = database.ChangeTracker.Entries<ApplicationCloneAuditDetail>().SingleOrDefault()?.Entity;
        if (!Armed || Fired || detail is null)
        {
            return result;
        }
        var affected = 0;
        switch (defect)
        {
            case ApplicationCloneDefect.Provenance:
                affected = await database.Set<ApplicationCloneAuditDetail>().Where(row => row.AuditId == detail.AuditId)
                    .ExecuteUpdateAsync(update => update.SetProperty(row => row.SourceApplicationId, Substitution), cancellationToken);
                break;
            case ApplicationCloneDefect.SourcePolicy:
                affected = await database.Set<ApplicationPolicy>().Where(row => row.ApplicationId == detail.SourceApplicationId)
                    .ExecuteUpdateAsync(update => update.SetProperty(row => row.Enabled, false), cancellationToken);
                break;
            case ApplicationCloneDefect.SourceNative:
                affected = await database.Set<OpenIddictEntityFrameworkCoreApplication>().Where(row => row.Id == detail.SourceApplicationId)
                    .ExecuteUpdateAsync(update => update.SetProperty(row => row.DisplayName, Substitution), cancellationToken);
                break;
            case ApplicationCloneDefect.Catalogue:
                affected = await database.Set<ScopePolicy>().Where(row => row.ScopeId == ScopeId)
                    .ExecuteUpdateAsync(update => update.SetProperty(row => row.Revision, row => row.Revision + 1), cancellationToken);
                break;
        }
        Assert.Equal(1, affected);
        Fired = true;
        return result;
    }
}

/// <summary>Names independent corruption boundaries without using an exception as the expected fault itself.</summary>
public enum ApplicationCloneDefect
{
    Provenance, SourcePolicy, SourceNative, Catalogue
}
