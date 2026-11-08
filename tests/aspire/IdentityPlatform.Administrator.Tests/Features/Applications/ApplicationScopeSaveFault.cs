using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Observes a genuine persisted native assignment before substituting permissions or policy inside its still-uncommitted transaction.</summary>
internal sealed class ApplicationScopeSaveFault(int change) : SaveChangesInterceptor
{
    internal string? ApplicationId
    {
        get;
        set;
    }
    internal string? ScopeId
    {
        get;
        set;
    }
    internal string? AudienceId
    {
        get;
        set;
    }
    internal string[]? PersistedPermissions
    {
        get;
        private set;
    }
    private const string Substituted = "scp:substituted";

    private const string Original = "scp:orders.read";

    /// <summary>Arms only after setup and performs one independently observed SQL change; production verification must discover it without a thrown probe exception.</summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (ApplicationId is null || PersistedPermissions is not null)
        {
            return result;
        }
        var database = (PlatformDbContext)eventData.Context!;
        var native = await database.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking().SingleAsync(row => row.Id == ApplicationId, cancellationToken);
        PersistedPermissions = JsonSerializer.Deserialize<string[]>(native.Permissions!)!;
        if (change == 0)
        {
            var altered = JsonSerializer.Serialize(PersistedPermissions.Where(value => value != Original).Append(Substituted));
            await database.Set<OpenIddictEntityFrameworkCoreApplication>().Where(row => row.Id == ApplicationId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Permissions, altered), cancellationToken);
        }
        else if (change == 1)
        {
            await database.Set<ScopePolicy>().Where(row => row.ScopeId == ScopeId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Enabled, false), cancellationToken);
        }
        else
        {
            await database.Set<ApiAudience>().Where(row => row.Id == AudienceId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Enabled, false), cancellationToken);
        }
        return result;
    }
}
