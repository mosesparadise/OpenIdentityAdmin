using System.Collections.Immutable;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Freezes expected persisted values and one transient secret; ordinary diagnostic formatting never prints credential material.</summary>
internal sealed class ApplicationWriteState
{
    private readonly object application;
    private readonly string clientId;
    private readonly string displayName;
    private readonly ImmutableArray<string> redirects;
    private readonly long revision;
    private readonly long credentialRevision;
    private readonly long issuedAt;
    private readonly long expiresAt;
    private readonly long observedAt;
    private readonly string? secret;
    private readonly string? originalHash;
    internal string Id
    {
        get;
    }
    internal string Version => ApplicationVersion.Create(Id, revision);

    /// <summary>Copies native presentation and lifecycle values so later entity mutation cannot alter the expected commit.</summary>
    internal ApplicationWriteState(object application, OpenIddictApplicationDescriptor descriptor, ApplicationPolicy policy, string? secret)
    {
        this.application = application;
        clientId = descriptor.ClientId!;
        displayName = descriptor.DisplayName!;
        redirects = descriptor.RedirectUris.Select(uri => uri.OriginalString).Order(StringComparer.Ordinal).ToImmutableArray();
        Id = policy.ApplicationId;
        revision = policy.Revision;
        credentialRevision = policy.CredentialRevision;
        issuedAt = policy.CredentialIssuedAt;
        expiresAt = policy.CredentialExpiresAt;
        observedAt = policy.ObservedAt;
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
        if (await manager.GetIdAsync(application, token) != Id || !ManagedApplicationProfile.Matches(descriptor) ||
            descriptor.ClientId != clientId || descriptor.DisplayName != displayName ||
            !descriptor.RedirectUris.Select(uri => uri.OriginalString).Order(StringComparer.Ordinal).SequenceEqual(redirects) ||
            string.IsNullOrEmpty(descriptor.ClientSecret) || (secret is null && descriptor.ClientSecret != originalHash) ||
            (secret is not null && !await manager.ValidateClientSecretAsync(application, secret, token)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        await ApplicationMutationWriter.RequireMutableAsync(scope, Id, clientId, token);
        var row = await scope.Owner.RunAsync(async current => await ((PlatformDbContext)scope.Owner.Database).Set<ApplicationPolicy>()
            .AsNoTracking().SingleOrDefaultAsync(value => value.ApplicationId == Id, current), token);
        if (row is null || row.Profile != ApplicationEditorRules.Profile || row.Revision != revision || row.CredentialRevision != credentialRevision ||
            row.CredentialIssuedAt != issuedAt || row.CredentialExpiresAt != expiresAt || row.ObservedAt != observedAt)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Builds only the fixed safe projection; callers publish it only after commit and real child cleanup.</summary>
    internal ApplicationDetails Details(DateTimeOffset authorityExpiry) => new(Id, clientId, displayName, redirects, ApplicationEditorRules.Profile,
        true, new(DateTimeOffset.FromUnixTimeSeconds(issuedAt), DateTimeOffset.FromUnixTimeSeconds(expiresAt)), authorityExpiry);

    /// <summary>Returns the new credential only in the immediate successful response; no reusable plaintext receipt is persisted.</summary>
    internal ApplicationCredentialOutput? Credential(ApplicationDetails details) => secret is null ? null : new(details, secret);
}
