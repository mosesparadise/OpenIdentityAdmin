using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Admin.Api.Features.Catalogue;

/// <summary>Owns finite catalogue reads under current fixed read authority and rollback-only native selection through actual cleanup.</summary>
internal static class CatalogueReadOperation
{
    /// <summary>Returns no commit capability and never requires or renews fresh MFA for read-only catalogue observations.</summary>
    internal static async Task<CatalogueReadResult> ReadAsync(IServiceScopeFactory factory, ApiIdentity identity, CatalogueResource kind,
        string? id, Guid? job, ApplicationListQuery? query, ApiResourceProfile profile, TimeProvider clock, CancellationToken token)
    {
        await using var scope = factory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var database = services.GetRequiredService<PlatformDbContext>();
        await using var transaction = await CeremonyTransaction.OpenApiReadAsync(database, services.GetRequiredService<NativeOperation>(), identity.Subject, token);
        await services.GetRequiredService<ProtocolStorageSelection>().SelectApiReadAsync(transaction, database, identity.Subject);
        var permission = kind switch
        {
            CatalogueResource.Scope => ApiReadResource.Scopes,
            CatalogueResource.Audience => ApiReadResource.Audiences,
            _ => throw new InvalidOperationException()
        };
        var access = await ApiCurrentAccess.ReadAsync(services, database, transaction, identity, profile.Callback, clock, permission);
        if (access.Status != StatusCodes.Status200OK)
        {
            throw new ApplicationHttpRejected(access.Status);
        }
        var expiry = await ExpiryAsync(services, identity, access.SourceExpiry, clock, transaction.Token);
        var result = await ProjectAsync(services, database, kind, id, job, query, expiry, transaction.Token);
        transaction.RequireApiRead(database, identity.Subject);
        return result;
    }

    /// <summary>Includes current native expiry as well as original signed and source expiry; a read never lengthens an observed lifetime.</summary>
    private static async Task<DateTimeOffset> ExpiryAsync(IServiceProvider services, ApiIdentity identity, long sourceExpiry, TimeProvider clock, CancellationToken token)
    {
        var manager = services.GetRequiredService<IOpenIddictTokenManager>();
        var native = await manager.FindByIdAsync(identity.TokenId, token);
        var nativeExpiry = native is null ? null : await manager.GetExpirationDateAsync(native, token);
        if (nativeExpiry is null || nativeExpiry <= clock.GetUtcNow())
        {
            throw new ApplicationHttpRejected(StatusCodes.Status401Unauthorized);
        }
        return new[] { identity.ExpiresAt, nativeExpiry.Value, DateTimeOffset.FromUnixTimeSeconds(sourceExpiry) }.Min();
    }

    /// <summary>Selects exactly one typed projection; durable job status remains independent of a target that has already been deleted.</summary>
    private static async Task<CatalogueReadResult> ProjectAsync(IServiceProvider services, PlatformDbContext database, CatalogueResource kind,
        string? id, Guid? job, ApplicationListQuery? query, DateTimeOffset expiry, CancellationToken token)
    {
        if (job is { } jobId)
        {
            var row = await database.Set<CatalogueDeletion>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == jobId, token);
            var expectedKind = kind == CatalogueResource.Scope ? CatalogueVersion.Scope : CatalogueVersion.Audience;
            if (row is null || row.Kind != expectedKind)
            {
                throw new CatalogueRejected(CatalogueFailure.Missing);
            }
            return new(CatalogueDeletionSnapshot.Capture(row).Details(expiry), null, expiry);
        }
        if (id is null)
        {
            return new(await CatalogueListReader.ReadAsync(database, kind, query ?? throw new InvalidOperationException(), expiry, token), null, expiry);
        }
        if (kind == CatalogueResource.Scope)
        {
            var scope = await CatalogueScopeReader.ReadAsync(services, database, id, token);
            return new(scope.Details(await CatalogueNativeReferences.ScopeAsync(services, scope.Name, token), expiry), scope.Version, expiry);
        }
        var audience = await database.Set<ApiAudience>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, token);
        if (audience is null || audience.Id != id)
        {
            throw new CatalogueRejected(CatalogueFailure.Missing);
        }
        var snapshot = CatalogueAudience.Capture(audience);
        return new(snapshot.Details(await CatalogueNativeReferences.AudienceAsync(services, snapshot.Resource, token), expiry), snapshot.Version, expiry);
    }
}

/// <summary>Retains a finite immutable projection and its original expiry until actual rollback and scope disposal have finished.</summary>
internal sealed record CatalogueReadResult(object Output, string? Version, DateTimeOffset ExpiresAt);
