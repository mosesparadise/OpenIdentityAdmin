using System.Collections.Immutable;
using IdentityPlatform.Core.Features.Catalogue;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Retains only bounded immutable policy metadata; this candidate is not evidence of human consent or permission to issue.</summary>
internal sealed record ManagedScopeDecision(int Version, string ApplicationId, long ApplicationRevision, string PermissionsDigest,
    ImmutableArray<string> Requested, ImmutableArray<string> Granted, ImmutableArray<ManagedScopeEntry> Scopes, ManagedScopeAudience? Audience)
{
    internal ImmutableArray<string> Resources => Audience is not null && Scopes.Any(scope =>
        scope.Kind == CatalogueRules.Api && Granted.Contains(scope.Name, StringComparer.Ordinal)) ? [Audience.Resource] : [];
    internal bool DisclosesEmail => Scopes.Any(scope => scope.Kind == CatalogueRules.Identity &&
        scope.Disclosure == CatalogueRules.Email && Granted.Contains(scope.Name, StringComparer.Ordinal));
}

/// <summary>Binds exact native scope identity and complete current metadata without storing personal data or duplicated consent text.</summary>
internal sealed record ManagedScopeEntry(string Id, string Name, long Revision, string Kind, string Disclosure, string Binding);

/// <summary>Retains the original single resource mapping even if a later catalogue edit moves its scope to another audience.</summary>
internal sealed record ManagedScopeAudience(string Id, string Resource, long Revision, string Binding);
