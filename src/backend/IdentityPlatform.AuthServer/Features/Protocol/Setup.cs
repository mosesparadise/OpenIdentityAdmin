using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Authorize;
using IdentityPlatform.AuthServer.Features.Protocol.Keys;
using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.AuthServer.Features.Protocol.Token;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol;

internal static class Setup
{
    internal static IServiceCollection AddProtocol(this IServiceCollection services, IssuerCredentials credentials)
    {
        services.AddOpenIddict().AddServer(options =>
        {
            options.SetIssuer(credentials.Issuer);
            options.SetAuthorizationEndpointUris("/connect/authorize");
            options.SetTokenEndpointUris("/connect/token");
            options.SetJsonWebKeySetEndpointUris("/connect/jwks");
            options.AllowAuthorizationCodeFlow();
            options.RequireProofKeyForCodeExchange();
            options.RegisterScopes(OpenIddictConstants.Scopes.OpenId, PilotProfile.Scope);
            options.RegisterClaims(OpenIddictConstants.Claims.AuthenticationTime, OpenIddictConstants.Claims.Nonce, OpenIddictConstants.Claims.AccessTokenHash);
            options.AddIssuerKeyPolicy(credentials);
            options.DisableAccessTokenEncryption();
            options.SetAuthorizationCodeLifetime(TimeSpan.FromSeconds(60));
            options.SetAccessTokenLifetime(TimeSpan.FromMinutes(5));
            options.SetIdentityTokenLifetime(TimeSpan.FromMinutes(5));
            options.Configure(configuration =>
            {
                configuration.RequestTokenLifetime = TimeSpan.FromSeconds(PilotProfile.InteractionSeconds);
                configuration.CodeChallengeMethods.Clear();
                configuration.CodeChallengeMethods.Add(OpenIddictConstants.CodeChallengeMethods.Sha256);
                configuration.PromptValues.Clear();
                configuration.PromptValues.UnionWith([OpenIddictConstants.PromptValues.None, OpenIddictConstants.PromptValues.Login]);
                configuration.ResponseModes.Clear();
                configuration.ResponseModes.Add(OpenIddictConstants.ResponseModes.Query);
                configuration.ClientAuthenticationMethods.Clear();
                configuration.ClientAuthenticationMethods.Add(OpenIddictConstants.ClientAuthenticationMethods.ClientSecretBasic);
            });
            options.AddFormExtractionProtection().AddTokenExtraction();
            options.EnableAuthorizationRequestCaching();
            options.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableTokenEndpointPassthrough();
        });
        return services;
    }

    internal static WebApplication MapProtocol(this WebApplication app)
    {
        app.MapAuthorize().MapToken();
        return app;
    }
}
