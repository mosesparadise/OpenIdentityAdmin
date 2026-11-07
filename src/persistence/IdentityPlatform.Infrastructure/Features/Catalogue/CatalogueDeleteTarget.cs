using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Retains exact current catalogue target state and native references under the caller's governance serialization.</summary>
internal sealed record CatalogueDeleteTarget(CatalogueScope? Scope, CatalogueAudience? Audience)
{
    internal string Id => Scope?.Id ?? Audience!.Id;
    internal long Revision => Scope?.Revision ?? Audience!.Revision;
    internal string Kind => Scope is not null ? CatalogueVersion.Scope : CatalogueVersion.Audience;

    /// <summary>Reads only a supported managed target; unknown kinds cannot fall through to an audience mutation.</summary>
    internal static async Task<CatalogueDeleteTarget> ReadAsync(ProtocolStorageScope scope, string kind, string id, CancellationToken token) => kind switch
    {
        CatalogueVersion.Scope => new(await CatalogueScope.ReadAsync(scope, id, token), null),
        CatalogueVersion.Audience => new(null, await CatalogueAudience.ReadAsync(scope, id, token)),
        _ => throw ProtocolStorageOwner.Unavailable()
    };

    /// <summary>Requires the caller's exact resource version without accepting wildcard, wrong-kind or changed-target preconditions.</summary>
    internal void RequireVersion(string version)
    {
        if (!CatalogueVersion.Matches(version, Kind, Id, Revision))
        {
            throw new CatalogueRejected(CatalogueFailure.Stale);
        }
    }

    /// <summary>Checks all current native dependencies, including retained revoked authorizations and unmanaged native mappings.</summary>
    internal Task<bool> ReferencedAsync(ProtocolStorageScope scope, CancellationToken token) => Scope is not null
        ? CatalogueNativeReferences.ScopeAsync(scope.Services, Scope.Name, token)
        : CatalogueNativeReferences.AudienceAsync(scope.Services, Audience!.Resource, token);

    /// <summary>Verifies the entire original native/product snapshot again before deletion rather than comparing only its identifier.</summary>
    internal Task VerifyAsync(ProtocolStorageScope scope, CancellationToken token) => Scope is not null
        ? Scope.VerifyAsync(scope, token) : Audience!.VerifyAsync(scope, token).AsTask();

    /// <summary>Deletes product policy and its manager-owned native scope atomically; audiences never cascade into scope mappings.</summary>
    internal async Task DeleteAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        await VerifyAsync(scope, token);
        if (Scope is not null)
        {
            await scope.Owner.RunAsync(async current =>
            {
                var policy = await scope.Owner.Database.Set<ScopePolicy>().SingleAsync(row => row.ScopeId == Id, current);
                scope.Owner.Database.Remove(policy);
                await scope.Owner.Database.SaveChangesAsync(current);
            }, token, write: true);
            var manager = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
            var native = await manager.FindByIdAsync(Id, token) ?? throw ProtocolStorageOwner.Unavailable();
            await manager.DeleteAsync(native, token);
        }
        else
        {
            await scope.Owner.RunAsync(async current =>
            {
                var row = await scope.Owner.Database.Set<ApiAudience>().SingleAsync(value => value.Id == Id, current);
                scope.Owner.Database.Remove(row);
                await scope.Owner.Database.SaveChangesAsync(current);
            }, token, write: true);
        }
    }

    /// <summary>Requires actual absence after all native/product/audit saves; suppressed deletion can never produce a successful terminal job.</summary>
    internal async Task RequireAbsentAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        var present = await scope.Owner.RunAsync(async current => Scope is not null
            ? await scope.Owner.Database.Set<ScopePolicy>().AsNoTracking().AnyAsync(row => row.ScopeId == Id, current)
            : await scope.Owner.Database.Set<ApiAudience>().AsNoTracking().AnyAsync(row => row.Id == Id, current), token);
        if (present || Scope is not null && await scope.Services.GetRequiredService<IOpenIddictScopeManager>().FindByIdAsync(Id, token) is not null)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}
