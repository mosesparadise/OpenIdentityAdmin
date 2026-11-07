using IdentityPlatform.Core.Features.Catalogue;

namespace IdentityPlatform.Infrastructure.Features.Catalogue;

/// <summary>Freezes validated catalogue action and resource preconditions before authority acquisition or lock waits.</summary>
internal sealed class CatalogueIntent
{
    internal CatalogueAction Action
    {
        get;
    }
    internal CatalogueResource Resource
    {
        get;
    }
    internal string? Id
    {
        get;
    }
    internal string? Version
    {
        get;
    }
    internal AudienceInput? Audience
    {
        get;
    }
    internal ScopeInput? Scope
    {
        get;
    }

    /// <summary>Admits only feature-owned factories; immutable records contain no caller-owned mutable collections.</summary>
    private CatalogueIntent(CatalogueAction action, CatalogueResource resource, string? id, string? version, AudienceInput? audience, ScopeInput? scope)
    {
        Action = action;
        Resource = resource;
        Id = id;
        Version = version;
        Audience = audience;
        Scope = scope;
    }

    /// <summary>Validates a new audience without accepting a caller-chosen database identity or revision.</summary>
    internal static CatalogueIntent? Create(AudienceInput input) => CatalogueRules.Validate(input).Count == 0
        ? new(CatalogueAction.Create, CatalogueResource.Audience, null, null, input, null) : null;

    /// <summary>Binds a complete bounded audience edit to one exact opaque precondition.</summary>
    internal static CatalogueIntent? Edit(string id, string version, AudienceInput input) => Target(id, version) && CatalogueRules.Validate(input).Count == 0
        ? new(CatalogueAction.Edit, CatalogueResource.Audience, id, version, input, null) : null;

    /// <summary>Validates the finite scope policy; audience eligibility is established against current storage under governance.</summary>
    internal static CatalogueIntent? Create(ScopeInput input) => CatalogueRules.Validate(input).Count == 0
        ? new(CatalogueAction.Create, CatalogueResource.Scope, null, null, null, input) : null;

    /// <summary>Binds immutable scope identity and mutable policy to a current resource version before native writes.</summary>
    internal static CatalogueIntent? Edit(string id, string version, ScopeInput input) => Target(id, version) && CatalogueRules.Validate(input).Count == 0
        ? new(CatalogueAction.Edit, CatalogueResource.Scope, id, version, null, input) : null;

    /// <summary>Rejects malformed preconditions before opening persistence or consuming administrator authority.</summary>
    private static bool Target(string id, string version) => CatalogueRules.IsIdentifier(id) && CatalogueVersion.IsCanonical(version);
}

/// <summary>Restricts catalogue policy mutation to explicitly supported actions.</summary>
internal enum CatalogueAction
{
    Create, Edit
}

/// <summary>Selects a fixed permission, version namespace and native storage boundary without accepting arbitrary route strings.</summary>
internal enum CatalogueResource
{
    Scope, Audience
}

/// <summary>Reports a finite rejection without formatting native exceptions, private policy or request values.</summary>
internal sealed class CatalogueRejected(CatalogueFailure failure) : Exception
{
    internal CatalogueFailure Failure => failure;
}

/// <summary>Separates expected catalogue conflicts from unknown storage failures, which must fail unavailable.</summary>
internal enum CatalogueFailure
{
    Missing, Duplicate, Stale, Immutable, Unsupported, Referenced
}
