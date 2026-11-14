using System.Collections.Immutable;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Contains bounded plain-text consent metadata for the exact current immutable policy, without account values.</summary>
internal sealed record ManagedScopeDisclosure(ImmutableArray<ManagedConsentScope> Scopes)
{
    /// <summary>Reads current labels only under verified policy; metadata changes cannot silently alter an already bound consent form.</summary>
    internal static async ValueTask<ManagedScopeDisclosure> ReadAsync(ProtocolStorageScope scope, ManagedLoginInteraction row, CancellationToken token)
    {
        var decision = ManagedScopeBinding.Read(row);
        var items = ImmutableArray.CreateBuilder<ManagedConsentScope>();
        foreach (var entry in decision.Scopes)
        {
            var current = await CatalogueScope.ReadAsync(scope, entry.Id, token);
            if (entry.Binding != FactorBinding.Digest(current))
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            items.Add(new(current.Name, current.DisplayName, current.ConsentText, current.Resource,
                current.Kind == CatalogueRules.Identity && current.Disclosure == CatalogueRules.Email));
        }
        return new(items.ToImmutable());
    }
}

/// <summary>Projects only one scope's validated label, consent text, optional API resource and finite email disclosure.</summary>
internal sealed record ManagedConsentScope(string Name, string DisplayName, string Description, string? Resource, bool Email);
