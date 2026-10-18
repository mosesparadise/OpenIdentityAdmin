using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.AuthServer.Features.Protocol.Authorize;
using IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;
using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.AuthServer.Features.Protocol.Keys;
using IdentityPlatform.AuthServer.Features.Protocol.Ownership;
using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.AuthServer.Features.Protocol.Token;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol;

internal static class Setup
{
    /// <summary>Preserves the complete standalone pilot configuration.</summary>
    internal static IServiceCollection AddProtocol(this IServiceCollection services, IssuerCredentials credentials) =>
        services.AddProtocol(credentials, new ProtocolProfiles(true, null));

    /// <summary>Registers one native server using the explicitly enabled profile union and durable issuer keys.</summary>
    internal static IServiceCollection AddProtocol(this IServiceCollection services, IssuerCredentials credentials, ProtocolProfiles profiles)
    {
        services.AddSingleton(profiles);
        services.AddOpenIddict().AddServer(options =>
        {
            options.SetIssuer(credentials.Issuer);
            options.SetAuthorizationEndpointUris(ProtocolOptions.AuthorizePath);
            options.SetTokenEndpointUris(ProtocolOptions.TokenPath);
            options.SetJsonWebKeySetEndpointUris(ProtocolOptions.KeysPath);
            options.AllowAuthorizationCodeFlow();
            options.RequireProofKeyForCodeExchange();
            options.RegisterClaims(OpenIddictConstants.Claims.AuthenticationTime, OpenIddictConstants.Claims.Nonce, OpenIddictConstants.Claims.AccessTokenHash);
            options.AddIssuerKeyPolicy(credentials);
            options.DisableAccessTokenEncryption();
            options.SetAuthorizationCodeLifetime(TimeSpan.FromSeconds(AdministratorRegistrationPolicy.ProofLifetimeSeconds));
            options.SetAccessTokenLifetime(TimeSpan.FromSeconds(AdministratorRegistrationPolicy.TokenLifetimeSeconds));
            options.SetIdentityTokenLifetime(TimeSpan.FromSeconds(AdministratorRegistrationPolicy.TokenLifetimeSeconds));
            options.AddFormExtractionProtection().AddTokenExtraction();
            if (profiles.Pilot)
            {
                options.EnableAuthorizationRequestCaching();
            }
            if (profiles.Administrator is { } administrator)
            {
                services.AddSingleton(administrator);
                options.SetPushedAuthorizationEndpointUris(ProtocolOptions.ParPath);
                options.AddServerStoreSelection().AddAdministratorAssertions().AddAdministratorAccountIssuance();
                options.AddProtocolHosting();
            }
            options.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableTokenEndpointPassthrough();
            options.Configure(configuration => ProtocolOptions.Configure(configuration, profiles));
        });
        return services;
    }

    internal static WebApplication MapProtocol(this WebApplication app)
    {
        app.MapAuthorize().MapToken();
        return app;
    }
}
