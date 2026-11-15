using System.Collections.Immutable;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Freezes only reusable configuration and independent source/catalogue observations; no source credential is supplied to creation.</summary>
internal sealed class ApplicationCloneSource
{
    private readonly ApplicationWriteState source;
    private readonly ImmutableArray<string> redirects;
    private readonly ImmutableArray<string> scopes;

    /// <summary>Keeps source verification private and exposes only allowlisted callback/scope configuration to the new descriptor.</summary>
    private ApplicationCloneSource(ApplicationWriteState source, OpenIddictApplicationDescriptor descriptor, ImmutableArray<string> scopes)
    {
        this.source = source;
        this.scopes = scopes;
        redirects = descriptor.RedirectUris.Select(uri => uri.OriginalString).Order(StringComparer.Ordinal).ToImmutableArray();
    }

    /// <summary>Resolves current assignment eligibility after governance acquisition and binds it to the unchanged source aggregate.</summary>
    internal static async Task<ApplicationCloneSource> ReadAsync(ProtocolStorageScope scope, object application,
        OpenIddictApplicationDescriptor descriptor, ApplicationPolicy policy, ApplicationMutationCommand command, CancellationToken token)
    {
        if (ApplicationMutationWriter.Now(scope, command) < policy.ObservedAt)
        {
            throw new ApplicationMutationRejected(ApplicationMutationFailure.Expired);
        }
        var prefix = OpenIddictConstants.Permissions.Prefixes.Scope;
        var scopes = descriptor.Permissions.Where(value => value.StartsWith(prefix, StringComparison.Ordinal))
            .Select(value => value[prefix.Length..]).Order(StringComparer.Ordinal).ToImmutableArray();
        var selection = await ApplicationScopeSelection.ReadAsync(scope, scopes, token);
        var source = new ApplicationWriteState(application, descriptor, policy, null, selection);
        await source.VerifyAsync(scope, token);
        return new(source, descriptor, scopes);
    }

    /// <summary>Copies exact callbacks and eligible additional scope names into a newly constructed profile, never native secrets or arbitrary settings.</summary>
    internal void Apply(OpenIddictApplicationDescriptor destination)
    {
        destination.RedirectUris.UnionWith(redirects.Select(value => new Uri(value)));
        destination.Permissions.UnionWith(scopes.Select(name => OpenIddictConstants.Permissions.Prefixes.Scope + name));
    }

    /// <summary>Requires the original source/hash/policy and catalogue observations after all destination and audit saves.</summary>
    internal Task VerifyAsync(ProtocolStorageScope scope, CancellationToken token) => source.VerifyAsync(scope, token);
}
