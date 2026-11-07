using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Captures exact native scope and product policy without introducing a second writable resource mapping.</summary>
internal sealed record CatalogueScope(string Id, string Name, string DisplayName, string Kind, bool Enabled,
    string ConsentText, string? AudienceId, string? Resource, string Disclosure, long Revision)
{
    internal string Version => CatalogueVersion.Create(CatalogueVersion.Scope, Id, Revision);
    /// <summary>Reloads native state rather than trusting process-local manager caches, then resolves its current resource through the audience catalogue.</summary>
    internal static async Task<CatalogueScope> ReadAsync(ProtocolStorageScope scope, string id, CancellationToken token)
    {
        await RequireMutableAsync(scope, id, token);
        var manager = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
        var native = await manager.FindByIdAsync(id, token);
        if (native is null || !await ProtocolStorageRows.ReloadAsync(scope, native, token) || await manager.GetIdAsync(native, token) != id)
        {
            throw new CatalogueRejected(CatalogueFailure.Missing);
        }
        var descriptor = new OpenIddictScopeDescriptor();
        await manager.PopulateAsync(descriptor, native, token);
        var policy = await scope.Owner.RunAsync(current => new ValueTask<ScopePolicy?>(scope.Owner.Database.Set<ScopePolicy>()
            .AsNoTracking().SingleOrDefaultAsync(row => row.ScopeId == id, current)), token);
        var resource = descriptor.Resources.Count == 1 ? descriptor.Resources.Single() : null;
        var audience = resource is null ? null : await ReadAudienceAsync(scope, resource, token);
        return Capture(id, descriptor, policy, audience);
    }

    /// <summary>Shares only pure finite native/product validation between owned writes and rollback-only reads.</summary>
    internal static CatalogueScope Capture(string id, OpenIddictScopeDescriptor descriptor, ScopePolicy? policy, CatalogueAudience? audience)
    {
        if (!CatalogueRules.IsIdentifier(id) || policy is null || policy.ScopeId != id || policy.Revision <= 0 || descriptor.Resources.Count > 1 ||
            descriptor.Description is not null || descriptor.Descriptions.Count != 0 || descriptor.DisplayNames.Count != 0 || descriptor.Properties.Count != 0)
        {
            throw new CatalogueRejected(CatalogueFailure.Unsupported);
        }
        var resource = descriptor.Resources.SingleOrDefault();
        var input = new ScopeInput(descriptor.Name!, descriptor.DisplayName!, policy.Kind, policy.Enabled, policy.ConsentText, audience?.Id, policy.Disclosure);
        if (CatalogueRules.Validate(input).Count != 0 || (policy.Kind == CatalogueRules.Identity && resource is not null) || resource != audience?.Resource)
        {
            throw new CatalogueRejected(CatalogueFailure.Unsupported);
        }
        return new(id, input.Name, input.DisplayName, input.Kind, input.Enabled, input.ConsentText, audience?.Id, resource, input.Disclosure, policy.Revision);
    }

    /// <summary>Rejects persisted system scope identities even if native names have drifted away from their reserved spelling.</summary>
    internal static async ValueTask RequireMutableAsync(ProtocolStorageScope scope, string id, CancellationToken token)
    {
        var bound = await scope.Owner.RunAsync(async current =>
            await scope.Owner.Database.Set<AdministratorProtocolProfile>().AsNoTracking().AnyAsync(row => row.ScopeId == id, current) ||
            await scope.Owner.Database.Set<PilotManifest>().AsNoTracking().AnyAsync(row => row.ScopeId == id, current), token);
        if (bound)
        {
            throw new CatalogueRejected(CatalogueFailure.Unsupported);
        }
    }

    /// <summary>Resolves the exact native resource without accepting collation aliases or unrelated unmanaged scope metadata.</summary>
    private static async ValueTask<CatalogueAudience> ReadAudienceAsync(ProtocolStorageScope scope, string resource, CancellationToken token)
    {
        var row = await scope.Owner.RunAsync(current => new ValueTask<ApiAudience?>(scope.Owner.Database.Set<ApiAudience>()
            .AsNoTracking().SingleOrDefaultAsync(value => value.Resource == resource, current)), token);
        return row is not null && row.Resource == resource ? CatalogueAudience.Capture(row) : throw new CatalogueRejected(CatalogueFailure.Unsupported);
    }

    /// <summary>Detects omitted native saves or policy drift after internal manager/audit persistence, before the caller's only commit attempt.</summary>
    internal async Task VerifyAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        if (this != await ReadAsync(scope, Id, token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Returns finite policy and native resource metadata after current references and publication authority are separately established.</summary>
    internal ScopeDetails Details(bool referenced, DateTimeOffset expiry) => new(Id, Name, DisplayName, Kind, Enabled, ConsentText, AudienceId, Resource, Disclosure,
        Version, referenced, expiry);
}
