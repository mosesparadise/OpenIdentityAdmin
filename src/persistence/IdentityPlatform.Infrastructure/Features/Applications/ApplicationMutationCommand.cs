using System.Collections.Immutable;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

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
    internal ApplicationLifecycleReason? Reason
    {
        get;
    }

    internal AdministratorPublicKey? PublicKey
    {
        get; private init;
    }
    internal ImmutableArray<string> Origins { get; private init; } = [];
    internal ImmutableArray<string> Scopes { get; private init; } = [];

    /// <summary>Freezes one explicitly confirmed public JWK, exact target version and future credential expiry.</summary>
    internal static ApplicationMutationCommand? ReplaceKey(string id, string version, string publicKeyJson, string expires, bool confirmed, DateTimeOffset now) =>
        confirmed && Target(id, version) && ApplicationEditorRules.TryReadExpiry(expires, now, out var expiry) &&
        AdministratorPublicKey.Parse(publicKeyJson) is { } key ? new(ApplicationMutationKind.ReplaceKey, id, version, null, null, [], expiry)
        {
            PublicKey = key
        } : null;

    /// <summary>Captures only already validated immutable values; no caller-supplied permissions or plaintext credentials are accepted.</summary>
    private ApplicationMutationCommand(ApplicationMutationKind kind, string? id, string? version, string? clientId,
        string? displayName, IEnumerable<string> redirects, DateTimeOffset? expiry, ApplicationLifecycleReason? reason = null)
    {
        Kind = kind;
        ApplicationId = id;
        Version = version;
        ClientId = clientId;
        DisplayName = displayName;
        Redirects = redirects.ToImmutableArray();
        CredentialExpiry = expiry;
        Reason = reason;
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

    /// <summary>Freezes an approved service identity and credential deadline without creating interactive grant capabilities.</summary>
    internal static ApplicationMutationCommand? CreateMachine(ApplicationMachineCreate input, DateTimeOffset now) =>
        MachineClientRules.IsValid(input, now) && ApplicationEditorRules.TryReadExpiry(input.CredentialExpiresAt, now, out var expiry)
            ? new(ApplicationMutationKind.CreateMachine, null, null, input.ClientId, input.DisplayName, [], expiry) : null;

    /// <summary>Freezes a confidential machine registration with a local public key and a bounded credential lifetime.</summary>
    internal static ApplicationMutationCommand? CreateWithKey(ApplicationMachineCreate input, string publicKeyJson, DateTimeOffset now) =>
        Keyed(CreateMachine(input, now), publicKeyJson, ApplicationMutationKind.CreateMachineKey);

    /// <summary>Freezes a confidential code client with exact S256 callbacks and a local public authentication key.</summary>
    internal static ApplicationMutationCommand? CreateWithKey(ApplicationCreate input, string publicKeyJson, DateTimeOffset now) =>
        Keyed(Create(input, now), publicKeyJson, ApplicationMutationKind.CreateWebKey);

    /// <summary>Uses only the validated original registration and the parsed public key for a new keyed application.</summary>
    private static ApplicationMutationCommand? Keyed(ApplicationMutationCommand? original, string json, ApplicationMutationKind kind) =>
        original is not null && AdministratorPublicKey.Parse(json) is { } key
            ? new(kind, null, null, original.ClientId, original.DisplayName, original.Redirects, original.CredentialExpiry)
            {
                PublicKey = key
            } : null;

    /// <summary>Snapshots a public client without accepting a credential or arbitrary protocol settings.</summary>
    internal static ApplicationMutationCommand? CreateNative(ApplicationNativeCreate input)
    {
        var frozen = input with
        {
            RedirectUris = input.RedirectUris?.ToArray()!
        };
        return ApplicationEditorRules.Validate(frozen).Count == 0
            ? new(ApplicationMutationKind.CreateNative, null, null, frozen.ClientId, frozen.DisplayName, frozen.RedirectUris, null) : null;
    }

    /// <summary>Snapshots callback and origin sets independently before acquiring database ownership.</summary>
    internal static ApplicationMutationCommand? CreateBrowser(ApplicationBrowserCreate input)
    {
        var frozen = input with
        {
            RedirectUris = input.RedirectUris?.ToArray()!,
            Origins = input.Origins?.ToArray()!
        };
        return ApplicationEditorRules.Validate(frozen).Count == 0
            ? new(ApplicationMutationKind.CreateBrowser, null, null, frozen.ClientId, frozen.DisplayName, frozen.RedirectUris, null)
            {
                Origins = frozen.Origins.Order(StringComparer.Ordinal).ToImmutableArray()
            } : null;
    }

    /// <summary>Binds confirmed new identity and expiry to an exact source version; reusable configuration is observed only under ownership.</summary>
    internal static ApplicationMutationCommand? Clone(string id, string version, ApplicationClone input, DateTimeOffset now) =>
        Target(id, version) && ApplicationCloneRules.IsValid(input, now) &&
        ApplicationEditorRules.TryReadExpiry(input.CredentialExpiresAt, now, out var expiry)
            ? new(ApplicationMutationKind.Clone, id, version, input.ClientId, input.DisplayName, [], expiry) : null;

    /// <summary>Allows edits to presentation and exact redirects only, bound to one canonical resource precondition.</summary>
    internal static ApplicationMutationCommand? Edit(string id, string version, ApplicationEdit input)
    {
        var frozen = input with
        {
            RedirectUris = input.RedirectUris?.ToArray()!
        };
        return Target(id, version) && (MachineClientRules.IsEdit(frozen) || ApplicationEditorRules.Validate(frozen).Count == 0)
            ? new(ApplicationMutationKind.Edit, id, version, null, frozen.DisplayName, frozen.RedirectUris, null) : null;
    }

    /// <summary>Requires explicit confirmation and a new future expiry without accepting or retaining a previous plaintext credential.</summary>
    internal static ApplicationMutationCommand? Replace(string id, string version, ApplicationSecretReplacement input, DateTimeOffset now) =>
        Target(id, version) && ApplicationEditorRules.Validate(input, now).Count == 0 && ApplicationEditorRules.TryReadExpiry(input.CredentialExpiresAt, now, out var expiry)
            ? new(ApplicationMutationKind.Replace, id, version, null, null, [], expiry) : null;

    /// <summary>Freezes the complete explicit assignment before waiting for current native authority; empty means removal.</summary>
    internal static ApplicationMutationCommand? AssignScopes(string id, string version, ApplicationScopeInput input)
    {
        var names = input.Scopes?.ToArray();
        return Target(id, version) && CatalogueRules.IsAssignment(names)
            ? new(ApplicationMutationKind.AssignScopes, id, version, null, null, [], null)
            {
                Scopes = names!.Order(StringComparer.Ordinal).ToImmutableArray()
            } : null;
    }

    /// <summary>Rejects unsupported resource identifiers and precondition aliases before persistence.</summary>
    private static bool Target(string id, string version) => ApplicationEditorRules.IsText(id, ApplicationProjection.IdentifierLength) && ApplicationVersion.IsCanonical(version);

    /// <summary>Binds an exact target/version to a confirmed lifecycle action and finite reason; no credential or grant mutation is included.</summary>
    internal static ApplicationMutationCommand? Lifecycle(string id, string version, ApplicationLifecycle input, bool enabled) =>
        Target(id, version) && ApplicationLifecycleRules.Reason(input) is { } reason
            ? new(enabled ? ApplicationMutationKind.Enable : ApplicationMutationKind.Disable, id, version, null, null, [], null, reason) : null;
}

internal enum ApplicationMutationKind
{
    Create, Edit, Replace, Disable, Enable, AssignScopes, Clone, CreateNative, CreateBrowser, CreateMachine, ReplaceKey, CreateWebKey, CreateMachineKey
}

/// <summary>Reports only a finite rejection reason without exposing request values or native validation details.</summary>
internal sealed class ApplicationMutationRejected(ApplicationMutationFailure failure) : Exception
{
    internal ApplicationMutationFailure Failure => failure;
}

internal enum ApplicationMutationFailure
{
    Forbidden, Missing, Duplicate, Stale, Expired, Unsupported, SameState
}
