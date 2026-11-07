using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Stages manager-owned scope changes and exact product policy within the caller's governed native transaction.</summary>
internal static class CatalogueScopeWriter
{
    /// <summary>Preserves immutable name/kind and resolves at most one current eligible audience before staging native and product writes.</summary>
    internal static async Task<CatalogueScope> StageAsync(ProtocolStorageScope scope, CatalogueIntent intent, CancellationToken token)
    {
        if (scope.Owner.Transaction is null || intent.Resource != CatalogueResource.Scope || intent.Scope is not { } input)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var audience = input.AudienceId is null ? null : await CatalogueAudience.ReadAsync(scope, input.AudienceId, token);
        if (input.Enabled && audience is { Enabled: false })
        {
            throw new CatalogueRejected(CatalogueFailure.Unsupported);
        }
        var manager = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
        var descriptor = new OpenIddictScopeDescriptor { Name = input.Name, DisplayName = input.DisplayName };
        if (audience is not null)
        {
            descriptor.Resources.Add(audience.Resource);
        }
        var policy = intent.Action == CatalogueAction.Create
            ? await CreateAsync(scope, manager, descriptor, token)
            : await EditAsync(scope, manager, descriptor, intent, token);
        policy.Kind = input.Kind;
        policy.Enabled = input.Enabled;
        policy.ConsentText = input.ConsentText;
        policy.Disclosure = input.Disclosure;
        var expected = new CatalogueScope(policy.ScopeId, input.Name, input.DisplayName, input.Kind, input.Enabled, input.ConsentText,
            audience?.Id, audience?.Resource, input.Disclosure, policy.Revision);
        await scope.Owner.RunAsync(async current => { await scope.Owner.Database.SaveChangesAsync(current); }, token, write: true);
        return expected;
    }

    /// <summary>Never adopts an existing native scope; native uniqueness and product FK remain in the same owning transaction.</summary>
    private static async Task<ScopePolicy> CreateAsync(ProtocolStorageScope scope, IOpenIddictScopeManager manager,
        OpenIddictScopeDescriptor descriptor, CancellationToken token)
    {
        if (await manager.FindByNameAsync(descriptor.Name!, token) is not null)
        {
            throw new CatalogueRejected(CatalogueFailure.Duplicate);
        }
        var native = await manager.CreateAsync(descriptor, token);
        var id = await manager.GetIdAsync(native, token);
        if (!CatalogueRules.IsIdentifier(id))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await CatalogueScope.RequireMutableAsync(scope, id!, token);
        var policy = new ScopePolicy { ScopeId = id! };
        await scope.Owner.RunAsync(current =>
        {
            scope.Owner.Database.Add(policy);
            return ValueTask.CompletedTask;
        }, token, write: true);
        return policy;
    }

    /// <summary>Rejects stale versions and immutable identity changes before invoking the native manager update.</summary>
    private static async Task<ScopePolicy> EditAsync(ProtocolStorageScope scope, IOpenIddictScopeManager manager,
        OpenIddictScopeDescriptor descriptor, CatalogueIntent intent, CancellationToken token)
    {
        var before = await CatalogueScope.ReadAsync(scope, intent.Id!, token);
        if (!CatalogueVersion.Matches(intent.Version, CatalogueVersion.Scope, before.Id, before.Revision))
        {
            throw new CatalogueRejected(CatalogueFailure.Stale);
        }
        if (before.Name != descriptor.Name || before.Kind != intent.Scope!.Kind)
        {
            throw new CatalogueRejected(CatalogueFailure.Immutable);
        }
        var native = await manager.FindByIdAsync(before.Id, token) ?? throw ProtocolStorageOwner.Unavailable();
        await manager.UpdateAsync(native, descriptor, token);
        var policy = await scope.Owner.RunAsync(current => new ValueTask<ScopePolicy>(scope.Owner.Database.Set<ScopePolicy>()
            .SingleAsync(row => row.ScopeId == before.Id, current)), token);
        policy.Revision = checked(before.Revision + 1);
        return policy;
    }
}
