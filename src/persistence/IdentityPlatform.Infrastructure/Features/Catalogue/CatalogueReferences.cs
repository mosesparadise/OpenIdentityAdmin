using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Shares the same native and retained-interaction reference decision between detail projections and durable deletion.</summary>
internal static class CatalogueReferences
{
    /// <summary>Retains owned EF admission, failure tracking and cleanup while native stores keep their independent dispatch wrapper.</summary>
    internal static async Task<bool> ScopeAsync(ProtocolStorageScope scope, string id, string name, CancellationToken token) =>
        await scope.Owner.RunAsync(current => new ValueTask<bool>(scope.Owner.Database.Set<ManagedScopeReference>().AsNoTracking()
            .AnyAsync(row => row.ScopeId == id, current)), token) || await CatalogueNativeReferences.ScopeAsync(scope.Services, name, token);

    /// <summary>Reads retained audience ownership through the current transaction's original bounded lifetime.</summary>
    internal static async Task<bool> AudienceAsync(ProtocolStorageScope scope, string id, string resource, CancellationToken token) =>
        await scope.Owner.RunAsync(current => new ValueTask<bool>(scope.Owner.Database.Set<ManagedScopeReference>().AsNoTracking()
            .AnyAsync(row => row.AudienceId == id, current)), token) || await CatalogueNativeReferences.AudienceAsync(scope.Services, resource, token);

    /// <summary>Keeps original scope identities referenced after application permissions or native scope names change.</summary>
    internal static async Task<bool> ScopeAsync(IServiceProvider services, string id, string name, CancellationToken token) =>
        await services.GetRequiredService<PlatformDbContext>().Set<ManagedScopeReference>().AsNoTracking().AnyAsync(row => row.ScopeId == id, token) ||
        await CatalogueNativeReferences.ScopeAsync(services, name, token);

    /// <summary>Keeps original audience identities referenced after native resource mappings move to another audience.</summary>
    internal static async Task<bool> AudienceAsync(IServiceProvider services, string id, string resource, CancellationToken token) =>
        await services.GetRequiredService<PlatformDbContext>().Set<ManagedScopeReference>().AsNoTracking().AnyAsync(row => row.AudienceId == id, token) ||
        await CatalogueNativeReferences.AudienceAsync(services, resource, token);
}
