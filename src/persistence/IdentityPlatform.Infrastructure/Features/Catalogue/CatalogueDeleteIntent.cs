using System.Security.Cryptography;
using System.Text;
using IdentityPlatform.Core.Features.Catalogue;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Freezes explicit deletion confirmation, resource/version and canonical request identity before current authority is acquired.</summary>
internal sealed class CatalogueDeleteIntent
{
    private const string GuidFormat = "D";
    private const string Delete = "catalogue-delete";
    private const string Separator = "\0";
    internal CatalogueResource Resource
    {
        get;
    }
    internal string Kind => Resource == CatalogueResource.Scope ? CatalogueVersion.Scope : CatalogueVersion.Audience;
    internal string Id
    {
        get;
    }
    internal string Version
    {
        get;
    }
    internal Guid RequestKey
    {
        get;
    }

    /// <summary>Retains only immutable validated values; native reference and current version checks remain transactional.</summary>
    private CatalogueDeleteIntent(CatalogueResource resource, string id, string version, Guid key)
    {
        Resource = resource;
        Id = id;
        Version = version;
        RequestKey = key;
    }

    /// <summary>Rejects missing confirmation, unsupported resource kinds and identifier/precondition/request-key aliases.</summary>
    internal static CatalogueDeleteIntent? Create(CatalogueResource resource, string id, string version, CatalogueDeleteInput input) =>
        Enum.IsDefined(resource) && CatalogueRules.IsIdentifier(id) && CatalogueVersion.IsCanonical(version) && input.Confirmed &&
        Guid.TryParseExact(input.RequestKey, GuidFormat, out var key) && key != Guid.Empty && key.ToString(GuidFormat) == input.RequestKey
            ? new(resource, id, version, key) : null;

    /// <summary>Scopes idempotency to the exact actor and target; hash values never grant authority independently.</summary>
    internal string ScopeHash(string actor) => Hash(Delete, actor, Kind, Id, RequestKey.ToString(GuidFormat));

    /// <summary>Binds the original resource version so a replay key cannot authorize a different mutation.</summary>
    internal string RequestHash() => Hash(Delete, Kind, Id, Version);

    /// <summary>Uses unambiguous framing for already validated values without emitting submitted identifiers in diagnostics.</summary>
    private static string Hash(params string[] values) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(Separator, values))));
}

/// <summary>Copies already verified source bindings and the earliest original authority deadline for durable execution; it is not a commit capability.</summary>
internal sealed record CatalogueDeletionApproval(string ActorId, string Stamp, long Generation, long FactorGeneration,
    long EnrollmentRevision, long FactorVerifiedAt, long ApprovalUntil);
