using IdentityPlatform.Core.Features.Applications;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.Applications;

/// <summary>Defines exactly the managed confidential web profile; native managers remain responsible for secret hashing and protocol metadata.</summary>
internal static class ManagedApplicationProfile
{
    private static readonly string[] Permissions = [OpenIddictConstants.Permissions.Endpoints.Authorization,
        OpenIddictConstants.Permissions.Endpoints.Token, OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
        OpenIddictConstants.Permissions.ResponseTypes.Code];
    private static readonly string[] Requirements = [OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange];

    /// <summary>Builds a native descriptor from immutable validated intent without allowing arbitrary grants, scopes or settings.</summary>
    internal static OpenIddictApplicationDescriptor Create(ApplicationMutationCommand command, string secret)
    {
        var result = new OpenIddictApplicationDescriptor
        {
            ApplicationType = OpenIddictConstants.ApplicationTypes.Web,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
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
        value.ApplicationType == OpenIddictConstants.ApplicationTypes.Web && value.ClientType == OpenIddictConstants.ClientTypes.Confidential &&
        value.ConsentType == OpenIddictConstants.ConsentTypes.Explicit && ApplicationEditorRules.IsClientId(value.ClientId) &&
        ApplicationEditorRules.IsText(value.DisplayName, ApplicationProjection.DisplayNameLength) &&
        value.RedirectUris.Count is > 0 and <= ApplicationEditorRules.MaximumRedirects && value.RedirectUris.All(uri => ApplicationEditorRules.IsRedirect(uri.OriginalString)) &&
        value.Permissions.SetEquals(Permissions) && value.Requirements.SetEquals(Requirements) && value.PostLogoutRedirectUris.Count == 0 &&
        value.JsonWebKeySet is null && value.Properties.Count == 0 && value.Settings.Count == 0 && value.DisplayNames.Count == 0;
}
