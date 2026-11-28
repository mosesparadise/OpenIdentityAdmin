using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Core.Features.Catalogue;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Defines the managed confidential web and public native descriptors used for registration and current-profile checks.</summary>
internal static class ManagedApplicationProfile
{
    private static readonly string[] Permissions = [OpenIddictConstants.Permissions.Endpoints.Authorization,
        OpenIddictConstants.Permissions.Endpoints.Token, OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
        OpenIddictConstants.Permissions.ResponseTypes.Code];
    private static readonly string[] Requirements = [OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange];
    internal static string OpenIdPermissionsDigest => FactorBinding.Digest(Permissions.Order(StringComparer.Ordinal).ToArray());

    /// <summary>Builds a native descriptor from immutable validated intent without allowing arbitrary grants, scopes or settings.</summary>
    internal static OpenIddictApplicationDescriptor Create(ApplicationMutationCommand command, string? secret)
    {
        var native = command.Kind == ApplicationMutationKind.CreateNative;
        var result = new OpenIddictApplicationDescriptor
        {
            ApplicationType = native ? OpenIddictConstants.ApplicationTypes.Native : OpenIddictConstants.ApplicationTypes.Web,
            ClientType = command.Kind is ApplicationMutationKind.CreateNative or ApplicationMutationKind.CreateBrowser ? OpenIddictConstants.ClientTypes.Public : OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            ClientId = command.ClientId,
            ClientSecret = secret,
            DisplayName = command.DisplayName
        };
        result.Permissions.UnionWith(Permissions);
        result.Requirements.UnionWith(Requirements);
        result.RedirectUris.UnionWith(command.Redirects.Select(value => new Uri(value)));
        return result;
    }

    /// <summary>Rejects native configuration drift instead of adopting arbitrary registrations into the supported write profile.</summary>
    internal static bool Matches(OpenIddictApplicationDescriptor value) =>
        MatchesManagement(value) && value.Permissions.SetEquals(Permissions);

    /// <summary>Allows only finite additional scope permissions for administration; this does not activate extended issuer eligibility.</summary>
    internal static bool MatchesManagement(OpenIddictApplicationDescriptor value) =>
        value.ApplicationType == OpenIddictConstants.ApplicationTypes.Web && value.ClientType == OpenIddictConstants.ClientTypes.Confidential &&
        MatchesCodeProfile(value, ApplicationEditorRules.IsRedirect);

    /// <summary>Requires an exact association between persisted profile and the native authentication and grant configuration.</summary>
    internal static bool MatchesProfile(string profile, OpenIddictApplicationDescriptor value) => profile switch
    {
        ApplicationEditorRules.BrowserProfile => value.ApplicationType == OpenIddictConstants.ApplicationTypes.Web &&
            value.ClientType == OpenIddictConstants.ClientTypes.Public && value.ClientSecret is null && MatchesCodeProfile(value, ApplicationEditorRules.IsRedirect),
        ApplicationEditorRules.NativeProfile => MatchesNative(value),
        ApplicationEditorRules.Profile => MatchesManagement(value) && !string.IsNullOrEmpty(value.ClientSecret),
        _ => false
    };

    /// <summary>Requires native public S256 with an OS-associated HTTPS callback and no client credential.</summary>
    internal static bool MatchesNative(OpenIddictApplicationDescriptor value) =>
        value.ApplicationType == OpenIddictConstants.ApplicationTypes.Native && value.ClientType == OpenIddictConstants.ClientTypes.Public &&
        value.ClientSecret is null && MatchesCodeProfile(value, ApplicationEditorRules.IsNativeRedirect);

    /// <summary>Validates the exact common code grant before applying a profile-specific callback policy.</summary>
    private static bool MatchesCodeProfile(OpenIddictApplicationDescriptor value, Func<string?, bool> callback) =>
        value.ConsentType == OpenIddictConstants.ConsentTypes.Explicit && ApplicationEditorRules.IsClientId(value.ClientId) &&
        ApplicationEditorRules.IsText(value.DisplayName, ApplicationProjection.DisplayNameLength) &&
        value.RedirectUris.Count is > 0 and <= ApplicationEditorRules.MaximumRedirects && value.RedirectUris.All(uri => callback(uri.OriginalString)) &&
        value.Permissions.IsSupersetOf(Permissions) && AdditionalPermissions(value.Permissions) && value.Requirements.SetEquals(Requirements) && value.PostLogoutRedirectUris.Count == 0 &&
        value.JsonWebKeySet is null && value.Properties.Count == 0 && value.Settings.Count == 0 && value.DisplayNames.Count == 0;

    /// <summary>Rejects extra endpoints/grants and reserved or oversized scope names without requiring disabled assignments to disappear.</summary>
    private static bool AdditionalPermissions(HashSet<string> permissions)
    {
        var additional = permissions.Except(Permissions, StringComparer.Ordinal).ToArray();
        var prefix = OpenIddictConstants.Permissions.Prefixes.Scope;
        return additional.All(value => value.StartsWith(prefix, StringComparison.Ordinal)) &&
            CatalogueRules.IsAssignment(additional.Select(value => value[prefix.Length..]).ToArray());
    }
}
