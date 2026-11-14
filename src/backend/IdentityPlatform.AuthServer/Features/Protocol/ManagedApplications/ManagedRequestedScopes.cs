using System.Collections.Immutable;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

namespace IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;

/// <summary>Parses a finite exact scope list before native caching; duplicates and whitespace aliases never become authority.</summary>
internal static class ManagedRequestedScopes
{
    private const char Separator = ' ';
    private const int MaximumLength = (CatalogueRules.MaximumScopes + 1) * (CatalogueRules.NameLength + 1);

    /// <summary>Returns immutable ordinal scope membership only when implicit OpenID and every unique bounded name are present.</summary>
    internal static ImmutableArray<string>? Read(string? value)
    {
        if (value is not { Length: > 0 and <= MaximumLength })
        {
            return null;
        }
        var names = value.Split(Separator).Order(StringComparer.Ordinal).ToImmutableArray();
        return ManagedScopeCodec.Names(names) ? names : null;
    }
}
