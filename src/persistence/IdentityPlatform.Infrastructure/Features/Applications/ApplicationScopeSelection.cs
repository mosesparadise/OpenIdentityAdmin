using System.Collections.Immutable;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Freezes current catalogue eligibility under governance and verifies it after native writes without duplicating authoritative assignments.</summary>
internal sealed class ApplicationScopeSelection(ImmutableArray<ApplicationScopeSelection.Entry> entries)
{
    /// <summary>Resolves exact ordinal names through native managers and independently reads current managed scope and audience policy.</summary>
    internal static async Task<ApplicationScopeSelection> ReadAsync(ProtocolStorageScope scope, ImmutableArray<string> names, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictScopeManager>();
        var entries = ImmutableArray.CreateBuilder<Entry>();
        try
        {
            foreach (var name in names)
            {
                var native = await manager.FindByNameAsync(name, token);
                var id = native is null ? null : await manager.GetIdAsync(native, token);
                if (id is null)
                {
                    throw new ApplicationMutationRejected(ApplicationMutationFailure.Unsupported);
                }
                var selected = await CatalogueScope.ReadAsync(scope, id, token);
                var audience = selected.AudienceId is null ? null : await CatalogueAudience.ReadAsync(scope, selected.AudienceId, token);
                if (selected.Name != name || !selected.Enabled || audience is { Enabled: false })
                {
                    throw new ApplicationMutationRejected(ApplicationMutationFailure.Unsupported);
                }
                entries.Add(new(selected, audience));
            }
        }
        catch (CatalogueRejected) { throw new ApplicationMutationRejected(ApplicationMutationFailure.Unsupported); }
        return new(entries.ToImmutable());
    }

    /// <summary>Rejects scope or audience substitution, disablement and revision changes after saves before the owning transaction may commit.</summary>
    internal async Task VerifyAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        foreach (var entry in entries)
        {
            await entry.Scope.VerifyAsync(scope, token);
            if (entry.Audience is { } audience)
            {
                await audience.VerifyAsync(scope, token);
            }
        }
    }

    /// <summary>Retains immutable nonsecret policy snapshots, never native entity references or a second persisted permission representation.</summary>
    internal sealed record Entry(CatalogueScope Scope, CatalogueAudience? Audience);
}
