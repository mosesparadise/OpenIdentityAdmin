using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Defines the exact native fixed registration shared by explicit initialization and live locked verification.</summary>
internal sealed class AdministratorRegistrationPolicy
{
    internal const string ProfileName = "administrator-v1";
    internal const string ProfileProperty = "profile";
    internal const string EnabledProperty = "enabled";
    internal const int ProofLifetimeSeconds = 60;
    internal const int TokenLifetimeSeconds = 300;
    private const string BrowserProfileName = "administrator-browser-v1";
    private const string LogoutReturnPath = "/bff/logout/return";
    private const int MaximumUriLength = 512;
    private const int PropertyCount = 2;
    private readonly Uri? logoutReturn;
    internal string RedirectUri
    {
        get;
    }
    internal AdministratorPublicKey Key
    {
        get;
    }

    /// <summary>Captures validated immutable configuration, never mutable manager entities or caller-selected destinations.</summary>
    private AdministratorRegistrationPolicy(string redirect, AdministratorPublicKey key, Uri? logoutReturn = null)
    {
        RedirectUri = redirect;
        Key = key;
        this.logoutReturn = logoutReturn;
    }

    /// <summary>Rejects noncanonical or non-HTTPS callbacks, wildcards, fragments and user information.</summary>
    internal static AdministratorRegistrationPolicy? Create(string redirect, AdministratorPublicKey key) =>
        ValidUri(redirect) ? new(redirect, key) : null;

    /// <summary>Derives a separate exact browser manifest; it neither upgrades persisted metadata nor changes B2 callers.</summary>
    internal AdministratorRegistrationPolicy? ForBrowserSessions()
    {
        var callback = new Uri(RedirectUri);
        var destination = new Uri(callback, LogoutReturnPath);
        var callbackPath = Uri.UnescapeDataString(callback.AbsolutePath).TrimEnd('/');
        return ValidUri(destination.AbsoluteUri) && !string.Equals(callbackPath, LogoutReturnPath, StringComparison.OrdinalIgnoreCase) ?
            new(RedirectUri, Key, destination) : null;
    }

    /// <summary>Bounds exact HTTPS destinations without accepting credentials, fragments, wildcards or normalization.</summary>
    private static bool ValidUri(string value) => value.Length <= MaximumUriLength && !value.Contains('*', StringComparison.Ordinal) &&
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        uri.UserInfo.Length == 0 && uri.Fragment.Length == 0 && uri.AbsoluteUri == value;

    /// <summary>Builds an independent exact descriptor for the selected variant, preserving mandatory PKCE/PAR and bounded lifetimes.</summary>
    internal OpenIddictApplicationDescriptor Application()
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ApplicationType = OpenIddictConstants.ApplicationTypes.Web,
            ClientId = AdministratorProtocolIdentity.ClientId,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            JsonWebKeySet = new JsonWebKeySet()
        };
        descriptor.JsonWebKeySet.Keys.Add(Key.CreateNativeKey());
        descriptor.RedirectUris.Add(new Uri(RedirectUri));
        descriptor.Permissions.UnionWith([OpenIddictConstants.Permissions.Endpoints.Authorization, OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.Endpoints.PushedAuthorization, OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.ResponseTypes.Code, OpenIddictConstants.Permissions.Prefixes.Scope + AdministratorProtocolIdentity.Scope]);
        descriptor.Requirements.UnionWith([OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange,
            OpenIddictConstants.Requirements.Features.PushedAuthorizationRequests]);
        descriptor.SetAuthorizationCodeLifetime(TimeSpan.FromSeconds(ProofLifetimeSeconds)).SetRequestTokenLifetime(TimeSpan.FromSeconds(ProofLifetimeSeconds))
            .SetAccessTokenLifetime(TimeSpan.FromSeconds(TokenLifetimeSeconds)).SetIdentityTokenLifetime(TimeSpan.FromSeconds(TokenLifetimeSeconds));
        if (logoutReturn is not null)
        {
            descriptor.PostLogoutRedirectUris.Add(logoutReturn);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
        }
        Properties(descriptor.Properties, logoutReturn is null ? ProfileName : BrowserProfileName);
        return descriptor;
    }

    /// <summary>Builds the sole API scope/resource binding through the native scope descriptor.</summary>
    internal static OpenIddictScopeDescriptor Scope()
    {
        var descriptor = new OpenIddictScopeDescriptor { Name = AdministratorProtocolIdentity.Scope };
        descriptor.Resources.Add(AdministratorProtocolIdentity.Resource);
        Properties(descriptor.Properties);
        return descriptor;
    }

    /// <summary>Compares all security-relevant metadata ordinally; display labels do not confer authority.</summary>
    internal bool Matches(OpenIddictApplicationDescriptor actual, OpenIddictScopeDescriptor scope)
    {
        var expected = Application();
        return actual.ApplicationType == expected.ApplicationType && actual.ClientId == expected.ClientId &&
            actual.ClientType == expected.ClientType && actual.ConsentType == expected.ConsentType && actual.ClientSecret is null &&
            actual.RedirectUris.Select(uri => uri.OriginalString).SequenceEqual([RedirectUri], StringComparer.Ordinal) &&
            actual.PostLogoutRedirectUris.Select(uri => uri.OriginalString).SequenceEqual(expected.PostLogoutRedirectUris.Select(uri => uri.OriginalString), StringComparer.Ordinal) &&
            actual.Permissions.SetEquals(expected.Permissions) && actual.Requirements.SetEquals(expected.Requirements) &&
            actual.Settings.Count == expected.Settings.Count && expected.Settings.All(pair => actual.Settings.TryGetValue(pair.Key, out var value) && value == pair.Value) &&
            actual.JsonWebKeySet is { Keys.Count: 1, AdditionalData.Count: 0 } keys && Key.Matches(keys.Keys[0]) &&
            Enabled(actual.Properties, logoutReturn is null ? ProfileName : BrowserProfileName) && scope.Name == AdministratorProtocolIdentity.Scope && Enabled(scope.Properties) &&
            scope.Resources.SetEquals([AdministratorProtocolIdentity.Resource]);
    }

    /// <summary>Requires exactly the fixed enabled marker; unknown metadata cannot silently introduce a second policy.</summary>
    private static bool Enabled(Dictionary<string, JsonElement> properties, string expectedProfile = ProfileName) => properties.Count == PropertyCount &&
        properties.TryGetValue(ProfileProperty, out var profile) && profile.ValueKind == JsonValueKind.String && profile.GetString() == expectedProfile &&
        properties.TryGetValue(EnabledProperty, out var enabled) && enabled.ValueKind == JsonValueKind.True;

    /// <summary>Adds explicit policy markers owned by initialization, never inferred from a client identifier.</summary>
    private static void Properties(Dictionary<string, JsonElement> properties, string profile = ProfileName)
    {
        properties.Add(ProfileProperty, JsonSerializer.SerializeToElement(profile));
        properties.Add(EnabledProperty, JsonSerializer.SerializeToElement(true));
    }
}
