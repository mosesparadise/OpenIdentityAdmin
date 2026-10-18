using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Validates native-extracted discovery before JWKS retrieval and before any credential-bearing backchannel request.</summary>
internal sealed class NativeClientMetadata(NativeClientProfile profile) : IOpenIddictClientHandler<OpenIddictClientEvents.HandleConfigurationResponseContext>
{
    /// <summary>Preserves native parsing/issuer validation and rejects missing or incompatible fixed-profile metadata before accepting it.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.HandleConfigurationResponseContext context)
    {
        Require(context.Configuration, profile);
        var algorithms = (JsonElement)context.Response[OpenIddictConstants.Metadata.IdTokenSigningAlgValuesSupported];
        if (algorithms.ValueKind != JsonValueKind.Array || algorithms.EnumerateArray().Any(value => value.ValueKind != JsonValueKind.String) ||
            !algorithms.EnumerateArray().Any(value => value.GetString() == SecurityAlgorithms.RsaSha256))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>Pins all used destinations and explicitly required native capabilities; absent metadata never enables a fallback method.</summary>
    internal static void Require(OpenIddictConfiguration configuration, NativeClientProfile profile)
    {
        if (!Exact(configuration.Issuer, profile.Issuer) || !Exact(configuration.AuthorizationEndpoint, profile.AuthorizationEndpoint) ||
            !Exact(configuration.TokenEndpoint, profile.TokenEndpoint) || !Exact(configuration.PushedAuthorizationEndpoint, profile.ParEndpoint) ||
            !Exact(configuration.JsonWebKeySetUri, profile.KeysEndpoint) ||
            configuration.UserInfoEndpoint is not null || configuration.MtlsUserInfoEndpoint is not null ||
            !configuration.GrantTypesSupported.Contains(OpenIddictConstants.GrantTypes.AuthorizationCode) ||
            !configuration.ResponseTypesSupported.Contains(OpenIddictConstants.ResponseTypes.Code) ||
            !configuration.ResponseModesSupported.Contains(OpenIddictConstants.ResponseModes.Query) ||
            !configuration.CodeChallengeMethodsSupported.Contains(OpenIddictConstants.CodeChallengeMethods.Sha256) ||
            !configuration.TokenEndpointAuthMethodsSupported.Contains(OpenIddictConstants.ClientAuthenticationMethods.PrivateKeyJwt) ||
            !configuration.PushedAuthorizationEndpointAuthMethodsSupported.Contains(OpenIddictConstants.ClientAuthenticationMethods.PrivateKeyJwt))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Uses ordinal original URI spelling so native normalization cannot widen a configured endpoint binding.</summary>
    private static bool Exact(Uri? actual, Uri expected) => actual?.OriginalString == expected.AbsoluteUri;
}
