using System.Collections.Immutable;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Defines a public device registration independently of callback-based application profiles.</summary>
internal static class DeviceClientRegistration
{
    private const string InvalidRegistration = "The device registration is invalid.";
    private static readonly string[] Required = [OpenIddictConstants.Permissions.Endpoints.DeviceAuthorization,
        OpenIddictConstants.Permissions.Endpoints.Token, OpenIddictConstants.Permissions.GrantTypes.DeviceCode];

    /// <summary>Creates a native public registration with device and token endpoints and explicit end-user approval.</summary>
    internal static OpenIddictApplicationDescriptor Descriptor(string clientId, string displayName)
    {
        if (!ApplicationEditorRules.IsClientId(clientId) || !ApplicationEditorRules.IsText(displayName, ApplicationProjection.DisplayNameLength))
        {
            throw new InvalidOperationException(InvalidRegistration);
        }
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = displayName,
            ApplicationType = OpenIddictConstants.ApplicationTypes.Native,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit
        };
        descriptor.Permissions.UnionWith(Required);
        return descriptor;
    }

    /// <summary>Checks every supported native setting and permits only assigned catalogue scopes beyond the fixed device permissions.</summary>
    internal static bool Matches(OpenIddictApplicationDescriptor client)
    {
        if (!ApplicationEditorRules.IsClientId(client.ClientId) || !ApplicationEditorRules.IsText(client.DisplayName, ApplicationProjection.DisplayNameLength) ||
            client.ClientType != OpenIddictConstants.ClientTypes.Public || client.ApplicationType != OpenIddictConstants.ApplicationTypes.Native ||
            client.ConsentType != OpenIddictConstants.ConsentTypes.Explicit || client.ClientSecret is not null || client.JsonWebKeySet is not null ||
            client.Settings.Count != 0 || client.Properties.Count != 0 || client.Requirements.Count != 0 || client.PostLogoutRedirectUris.Count != 0 ||
            client.RedirectUris.Count != 0 || client.DisplayNames.Count != 0 || !client.Permissions.IsSupersetOf(Required))
        {
            return false;
        }
        var extras = client.Permissions.Except(Required, StringComparer.Ordinal).ToArray();
        var prefix = OpenIddictConstants.Permissions.Prefixes.Scope;
        return extras.All(value => value.StartsWith(prefix, StringComparison.Ordinal)) &&
            CatalogueRules.IsAssignment(extras.Select(value => value[prefix.Length..]).ToArray());
    }

    /// <summary>Loads the actual current OpenIddict application and policy under the governance lock shared with lifecycle changes.</summary>
    internal static async ValueTask<(ManagedRegistrationState State, ApplicationPolicy Policy, string DisplayName)?> ReadLockedAsync(ProtocolStorageScope scope, string clientId, CancellationToken token)
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var client = await manager.FindByClientIdAsync(clientId, token);
        if (client is null || !await ProtocolStorageRows.ReloadAsync(scope, client, token) ||
            await manager.GetIdAsync(client, token) is not { } id)
        {
            return null;
        }
        var policy = await scope.Owner.RunAsync(async current => await scope.Owner.Database.Set<ApplicationPolicy>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.ApplicationId == id, current), token);
        if (policy is not
            {
                Enabled: true, Retired: false, Profile: DevicePollingWindow.Profile, CredentialRevision: 0,
                CredentialIssuedAt: null, CredentialExpiresAt: null, Revision: > 0
            } || policy.ApplicationId != id)
        {
            return null;
        }
        _ = ManagedLoginRules.Observe(scope.Owner.Clock, policy.ObservedAt);
        var descriptor = new OpenIddictApplicationDescriptor();
        await manager.PopulateAsync(descriptor, client, token);
        if (descriptor.ClientId != clientId || !Matches(descriptor))
        {
            return null;
        }
        await ApplicationMutationWriter.RequireMutableAsync(scope, id, clientId, token);
        var permissions = descriptor.Permissions.Order(StringComparer.Ordinal).ToImmutableArray();
        return (new(new(id, clientId, policy.Revision, 0, null, policy.ObservedAt, [], policy.Profile), permissions, false), policy, descriptor.DisplayName!);
    }
}
