using System.Collections.Immutable;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Freezes validated mutation contents before any lock wait, preventing caller-owned collection changes from altering approved intent.</summary>
internal sealed class ApplicationMutationCommand
{
    internal ApplicationMutationKind Kind
    {
        get;
    }
    internal string? ApplicationId
    {
        get;
    }
    internal string? Version
    {
        get;
    }
    internal string? ClientId
    {
        get;
    }
    internal string? DisplayName
    {
        get;
    }
    internal ImmutableArray<string> Redirects
    {
        get;
    }
    internal DateTimeOffset? CredentialExpiry
    {
        get;
    }

    /// <summary>Captures only already validated immutable values; no caller-supplied permissions or plaintext credentials are accepted.</summary>
    private ApplicationMutationCommand(ApplicationMutationKind kind, string? id, string? version, string? clientId,
        string? displayName, IEnumerable<string> redirects, DateTimeOffset? expiry)
    {
        Kind = kind;
        ApplicationId = id;
        Version = version;
        ClientId = clientId;
        DisplayName = displayName;
        Redirects = redirects.ToImmutableArray();
        CredentialExpiry = expiry;
    }

    /// <summary>Copies the collection before validation and preserves the operator's exact future UTC expiry.</summary>
    internal static ApplicationMutationCommand? Create(ApplicationCreate input, DateTimeOffset now)
    {
        var frozen = input with
        {
            RedirectUris = input.RedirectUris?.ToArray()!
        };
        return ApplicationEditorRules.Validate(frozen, now).Count == 0 && ApplicationEditorRules.TryReadExpiry(frozen.CredentialExpiresAt, now, out var expiry)
            ? new(ApplicationMutationKind.Create, null, null, frozen.ClientId, frozen.DisplayName, frozen.RedirectUris, expiry) : null;
    }

    /// <summary>Allows edits to presentation and exact redirects only, bound to one canonical resource precondition.</summary>
    internal static ApplicationMutationCommand? Edit(string id, string version, ApplicationEdit input)
    {
        var frozen = input with
        {
            RedirectUris = input.RedirectUris?.ToArray()!
        };
        return Target(id, version) && ApplicationEditorRules.Validate(frozen).Count == 0
            ? new(ApplicationMutationKind.Edit, id, version, null, frozen.DisplayName, frozen.RedirectUris, null) : null;
    }

    /// <summary>Requires explicit confirmation and a new future expiry without accepting or retaining a previous plaintext credential.</summary>
    internal static ApplicationMutationCommand? Replace(string id, string version, ApplicationSecretReplacement input, DateTimeOffset now) =>
        Target(id, version) && ApplicationEditorRules.Validate(input, now).Count == 0 && ApplicationEditorRules.TryReadExpiry(input.CredentialExpiresAt, now, out var expiry)
            ? new(ApplicationMutationKind.Replace, id, version, null, null, [], expiry) : null;

    /// <summary>Rejects unsupported resource identifiers and precondition aliases before persistence.</summary>
    private static bool Target(string id, string version) => ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength) && ApplicationVersion.IsCanonical(version);
}

internal enum ApplicationMutationKind
{
    Create, Edit, Replace
}

/// <summary>Reports only a finite rejection reason without exposing request values or native validation details.</summary>
internal sealed class ApplicationMutationRejected(ApplicationMutationFailure failure) : Exception
{
    internal ApplicationMutationFailure Failure => failure;
}

internal enum ApplicationMutationFailure
{
    Forbidden, Missing, Duplicate, Stale, Expired, Unsupported
}
