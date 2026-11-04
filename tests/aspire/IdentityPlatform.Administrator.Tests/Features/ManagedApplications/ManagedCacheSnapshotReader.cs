using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Shares the existing ordered committed-state reads without depending on registration or browser creation.</summary>
internal static class ManagedCacheSnapshotReader
{
    /// <summary>Reads committed native and product state independently in a fresh provider context.</summary>
    internal static async Task<ManagedCacheSnapshot> ReadAsync(Func<AsyncServiceScope> createScope, string applicationId, bool applicationOnly = false)
    {
        await using var scope = createScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var rows = await database.Set<ManagedLoginInteraction>().AsNoTracking().Where(row => !applicationOnly || row.ApplicationId == applicationId).OrderBy(row => row.SelectorDigest).ToArrayAsync(TestContext.Current.CancellationToken);
        var tokens = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().Include(row => row.Authorization).Where(row => row.Application!.Id == applicationId)
            .OrderBy(row => row.Id).ToArrayAsync(TestContext.Current.CancellationToken);
        return new(rows, tokens, await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().CountAsync(row => row.Application!.Id == applicationId, TestContext.Current.CancellationToken),
            await database.Set<AdministratorSession>().CountAsync(TestContext.Current.CancellationToken));
    }

}
