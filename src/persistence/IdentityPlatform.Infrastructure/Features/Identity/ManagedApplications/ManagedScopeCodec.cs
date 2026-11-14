using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;

/// <summary>Accepts one bounded canonical representation; duplicate fields, reordered text and partial legacy bindings cannot gain authority.</summary>
internal static class ManagedScopeCodec
{
    internal const int FormatVersion = 1;
    private const int MaximumDepth = 8;
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        MaxDepth = MaximumDepth
    };

    /// <summary>Checks complete semantic bounds before serializing immutable arrays; UTF-8 byte limits are independent of UTF-16 storage bounds.</summary>
    internal static string Encode(ManagedScopeDecision decision)
    {
        Require(decision);
        var value = JsonSerializer.Serialize(decision, Options);
        if (Encoding.UTF8.GetByteCount(value) > CatalogueRules.MaximumDecisionBytes)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return value;
    }

    /// <summary>Rejects noncanonical JSON and invalid digests instead of normalizing or reconstructing an earlier decision.</summary>
    internal static ManagedScopeDecision Decode(string value, string digest)
    {
        if (string.IsNullOrEmpty(value) || value.Length > CatalogueRules.MaximumDecisionBytes ||
            Encoding.UTF8.GetByteCount(value) > CatalogueRules.MaximumDecisionBytes || !ManagedLoginRules.IsSecret(digest) ||
            ManagedLoginRules.Digest(value) != digest)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        try
        {
            var decision = JsonSerializer.Deserialize<ManagedScopeDecision>(value, Options) ?? throw ProtocolStorageOwner.Unavailable();
            if (Encode(decision) != value)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            return decision;
        }
        catch (JsonException) { throw ProtocolStorageOwner.Unavailable(); }
    }

    /// <summary>Requires ordinal sorted unique requested/granted sets containing implicit OpenID and at most 32 additional catalogue names.</summary>
    internal static bool Names(ImmutableArray<string> names) => !names.IsDefault && names.Length is > 0 and <= CatalogueRules.MaximumScopes + 1 &&
        names.Contains(OpenIddictConstants.Scopes.OpenId, StringComparer.Ordinal) &&
        names.All(value => value == OpenIddictConstants.Scopes.OpenId || CatalogueRules.IsName(value)) &&
        names.SequenceEqual(names.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal);

    /// <summary>Rejects inconsistent membership, kind/disclosure and audience combinations before any persisted snapshot can be used.</summary>
    private static void Require(ManagedScopeDecision value)
    {
        if (value.Version != FormatVersion || !ApplicationEditorRules.IsText(value.ApplicationId, ApplicationProjection.IdentifierLength) || value.ApplicationRevision <= 0 ||
            !ManagedLoginRules.IsSecret(value.PermissionsDigest) || !Names(value.Requested) || !Names(value.Granted) ||
            value.Granted.Except(value.Requested, StringComparer.Ordinal).Any() || value.Scopes.IsDefault ||
            value.Scopes.Length > CatalogueRules.MaximumScopes || value.Scopes.Any(scope => scope is null || !Entry(scope)) ||
            value.Scopes.Select(scope => scope.Id).Distinct(StringComparer.Ordinal).Count() != value.Scopes.Length ||
            !value.Scopes.Select(scope => scope.Name).SequenceEqual(value.Requested.Where(name => name != OpenIddictConstants.Scopes.OpenId), StringComparer.Ordinal) ||
            !Audience(value))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Admits only supported finite identity/API policy and a nonempty native metadata binding.</summary>
    private static bool Entry(ManagedScopeEntry value) => CatalogueRules.IsIdentifier(value.Id) && CatalogueRules.IsName(value.Name) &&
        value.Revision > 0 && ManagedLoginRules.IsSecret(value.Binding) && value.Kind is CatalogueRules.Identity or CatalogueRules.Api &&
        value.Disclosure is CatalogueRules.None or CatalogueRules.Email && (value.Kind == CatalogueRules.Identity || value.Disclosure == CatalogueRules.None) &&
        (!string.Equals(value.Name, CatalogueRules.Email, StringComparison.OrdinalIgnoreCase) || value.Name == CatalogueRules.Email &&
            value.Kind == CatalogueRules.Identity && value.Disclosure == CatalogueRules.Email);

    /// <summary>Requires exactly one audience record for any requested API scope and no fabricated resource for identity-only policy.</summary>
    private static bool Audience(ManagedScopeDecision value) => value.Scopes.Any(scope => scope.Kind == CatalogueRules.Api)
        ? value.Audience is { Revision: > 0 } audience && CatalogueRules.IsIdentifier(audience.Id) &&
            CatalogueRules.IsResource(audience.Resource) && ManagedLoginRules.IsSecret(audience.Binding)
        : value.Audience is null;
}
