using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Sample.Client.Features.Authentication;

/// <summary>Pins every used native discovery destination before key retrieval or credential-bearing transport.</summary>
internal sealed class SampleMetadata : IOpenIddictClientHandler<OpenIddictClientEvents.HandleConfigurationResponseContext>
{
    private const string AuthorizationEndpointValue = "connect/authorize";
    private const string TokenEndpointValue = "connect/token";
    private const string KeyEndpoint = "connect/jwks";
    private const string UserInfoEndpointValue = "connect/userinfo";

    /// <summary>Rejects substituted endpoints and missing capabilities rather than negotiating another client profile.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.HandleConfigurationResponseContext context)
    {
        var value = context.Configuration;
        if (value.Issuer?.OriginalString != SampleProfile.Issuer ||
            value.AuthorizationEndpoint?.OriginalString != SampleProfile.Issuer + AuthorizationEndpointValue ||
            value.TokenEndpoint?.OriginalString != SampleProfile.Issuer + TokenEndpointValue ||
            value.JsonWebKeySetUri?.OriginalString != SampleProfile.Issuer + KeyEndpoint ||
            value.UserInfoEndpoint?.OriginalString != SampleProfile.Issuer + UserInfoEndpointValue ||
            value.MtlsTokenEndpoint is not null || value.MtlsUserInfoEndpoint is not null ||
            !value.CodeChallengeMethodsSupported.Contains(OpenIddictConstants.CodeChallengeMethods.Sha256) ||
            !value.TokenEndpointAuthMethodsSupported.Contains(OpenIddictConstants.ClientAuthenticationMethods.ClientSecretBasic))
        {
            throw new InvalidOperationException(SampleProfile.Failure);
        }
        return ValueTask.CompletedTask;
    }
}
