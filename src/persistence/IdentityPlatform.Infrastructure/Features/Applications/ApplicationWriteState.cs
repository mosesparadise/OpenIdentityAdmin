using System.Collections.Immutable;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Freezes expected persisted values and one transient secret; ordinary diagnostic formatting never prints credential material.</summary>
internal sealed class ApplicationWriteState
{
    private readonly object application;
    private readonly AdministratorPublicKey? approvedKey;
    private readonly string clientId;
    private readonly string displayName;
    private readonly ImmutableArray<string> redirects;
    private readonly ImmutableArray<string> permissions;
    private readonly ApplicationScopeSelection? selection;
    private readonly ApplicationCloneSource? cloneSource;
    private readonly ImmutableArray<string> origins;
    private readonly long revision;
    private readonly long credentialRevision;
    private readonly string profile;
    private readonly long? issuedAt;
    private readonly long? expiresAt;
    private readonly long observedAt;
    private readonly bool enabled;
    private readonly bool retired;
    private readonly string? secret;
    private readonly string? originalHash;
    internal string Id
    {
        get;
    }
    internal long CredentialRevision => credentialRevision;
    internal string Version => ApplicationVersion.Create(Id, revision);

    /// <summary>Copies native presentation and lifecycle values so later entity mutation cannot alter the expected commit.</summary>
    internal ApplicationWriteState(object application, OpenIddictApplicationDescriptor descriptor, ApplicationPolicy policy, string? secret,
        ApplicationScopeSelection? selection = null, ApplicationCloneSource? cloneSource = null, ImmutableArray<string> origins = default, AdministratorPublicKey? approvedKey = null)
    {
        this.application = application;
        this.approvedKey = approvedKey;
        this.selection = selection;
        this.cloneSource = cloneSource;
        this.origins = origins;
        permissions = descriptor.Permissions.Order(StringComparer.Ordinal).ToImmutableArray();
        clientId = descriptor.ClientId!;
        displayName = descriptor.DisplayName!;
        redirects = descriptor.RedirectUris.Select(uri => uri.OriginalString).Order(StringComparer.Ordinal).ToImmutableArray();
        Id = policy.ApplicationId;
        revision = policy.Revision;
        credentialRevision = policy.CredentialRevision;
        profile = policy.Profile;
        issuedAt = policy.CredentialIssuedAt;
        expiresAt = policy.CredentialExpiresAt;
        observedAt = policy.ObservedAt;
        enabled = policy.Enabled;
        retired = policy.Retired;
        this.secret = secret;
        originalHash = secret is null ? descriptor.ClientSecret : null;
    }

    /// <summary>Reads fresh persisted rows after internal manager/audit saves and rejects unexpected metadata, identity or credential changes.</summary>
    internal async Task VerifyAsync(ProtocolStorageScope scope, CancellationToken token)
    {
        if (!await ProtocolStorageRows.ReloadAsync(scope, application, token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, application, token);
        if (await manager.GetIdAsync(application, token) != Id || !await MatchesProfileAsync(scope, descriptor, token) ||
            !descriptor.Permissions.Order(StringComparer.Ordinal).SequenceEqual(permissions, StringComparer.Ordinal) ||
            descriptor.ClientId != clientId || descriptor.DisplayName != displayName ||
            !descriptor.RedirectUris.Select(uri => uri.OriginalString).Order(StringComparer.Ordinal).SequenceEqual(redirects) ||
            (secret is null && descriptor.ClientSecret != originalHash) ||
            (secret is not null && !await manager.ValidateClientSecretAsync(application, secret, token)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        if (!origins.IsDefault && !origins.SequenceEqual(await ApplicationBrowserOrigin.ReadAsync(scope, Id, token), StringComparer.Ordinal))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        if (selection is not null)
        {
            await selection.VerifyAsync(scope, token);
        }
        if (cloneSource is not null)
        {
            await cloneSource.VerifyAsync(scope, token);
        }
        await ApplicationMutationWriter.RequireMutableAsync(scope, Id, clientId, token);
        var row = await scope.Owner.RunAsync(async current => await ((PlatformDbContext)scope.Owner.Database).Set<ApplicationPolicy>()
            .AsNoTracking().SingleOrDefaultAsync(value => value.ApplicationId == Id, current), token);
        if (row is null || row.Retired != retired || row.Enabled != enabled || row.Profile != profile || row.Revision != revision || row.CredentialRevision != credentialRevision ||
            row.CredentialIssuedAt != issuedAt || row.CredentialExpiresAt != expiresAt || row.ObservedAt != observedAt)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Verifies the pinned public material and approval revision again after the administrator's audit save.</summary>
    private async ValueTask<bool> MatchesProfileAsync(ProtocolStorageScope scope, OpenIddictApplicationDescriptor descriptor, CancellationToken token)
    {
        if (approvedKey is null)
        {
            return ManagedApplicationProfile.MatchesProfile(profile, descriptor);
        }
        var approval = await scope.Owner.RunAsync(async current => await scope.Owner.Database.Set<ApplicationKeyApproval>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.ApplicationId == Id, current), token);
        return ManagedApplicationProfile.MatchesKeyProfile(profile, descriptor) &&
            AdministratorPublicKey.FromNative(descriptor.JsonWebKeySet)?.Fingerprint == approvedKey.Fingerprint &&
            approval?.ApplicationId == Id && approval.CredentialRevision == credentialRevision && approval.Fingerprint == approvedKey.Fingerprint &&
            approval.Thumbprint == ApplicationKeyApproval.Material(approvedKey);
    }

    /// <summary>Builds only the fixed safe projection; callers publish it only after commit and real child cleanup.</summary>
    internal ApplicationDetails Details(DateTimeOffset authorityExpiry) => new(Id, clientId, displayName, redirects, profile,
        !retired, issuedAt is { } issued && expiresAt is { } expires ? new(DateTimeOffset.FromUnixTimeSeconds(issued), DateTimeOffset.FromUnixTimeSeconds(expires)) : null,
        authorityExpiry, enabled, retired, Origins: profile == ApplicationEditorRules.BrowserProfile ? origins : null);

    /// <summary>Projects the exact persisted permission expectation only after commit and cleanup, without changing existing editor wire contracts.</summary>
    internal ApplicationScopes Scopes(DateTimeOffset authorityExpiry) => new(Id, permissions
        .Where(value => value.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope, StringComparison.Ordinal))
        .Select(value => value[OpenIddictConstants.Permissions.Prefixes.Scope.Length..]).ToArray(), authorityExpiry);

    /// <summary>Returns the new credential only in the immediate successful response; no reusable plaintext receipt is persisted.</summary>
    internal ApplicationCredentialOutput? Credential(ApplicationDetails details) => secret is null ? null : new(details, secret);
}
