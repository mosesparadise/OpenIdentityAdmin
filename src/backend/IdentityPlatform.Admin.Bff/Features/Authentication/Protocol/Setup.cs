using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Ownership;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Offers dormant fixed-profile native composition; callers supply Sessions persistence and own credential lifetime.</summary>
internal static class Setup
{
    internal const string CorrelationPrefix = "__Host-identity-admin-client-state";
    private const string CookiePath = "/";
    private const int BackchannelSeconds = 10;
    private const int PolicyOrderOffset = 500;

    /// <summary>Registers only native protocol services; production Programs do not call this internal B2 extension.</summary>
    internal static IServiceCollection AddAdministratorClient(this IServiceCollection services, NativeClientProfile profile, NativeClientCredentials credentials)
    {
        services.AddSingleton(profile);
        services.AddSingleton(credentials);
        services.AddProtocolStorageOwnership();
        services.AddOwnedClientDispatcher().AddOwnedClientServiceScopes();
        services.AddOpenIddict().AddCore(core =>
        {
            core.UseEntityFrameworkCore().UseDbContext<AdminSessionDbContext>();
            core.DisableEntityCaching();
        });
        services.AddOpenIddict().AddClient(client => Configure(client, profile, credentials));
        return services;
    }

    /// <summary>Separates global protected-state credentials from the registration assertion key and restricts native algorithms.</summary>
    private static void Configure(OpenIddictClientBuilder client, NativeClientProfile profile, NativeClientCredentials credentials)
    {
        client.AllowAuthorizationCodeFlow().SetRedirectionEndpointUris(profile.Callback).AddDurableClientState().AddDurableClientConsumption();
        foreach (var certificate in credentials.StateSigning.All)
        {
            client.AddSigningCertificate(certificate);
        }
        foreach (var certificate in credentials.StateEncryption.All)
        {
            client.AddEncryptionCertificate(certificate);
        }
        client.Configure(options =>
        {
            options.ClientUri = profile.ClientUri;
            options.StateTokenLifetime = TimeSpan.FromSeconds(AdministratorRegistrationPolicy.TokenLifetimeSeconds);
            options.ClientAssertionLifetime = TimeSpan.FromSeconds(AdministratorRegistrationPolicy.ProofLifetimeSeconds);
            options.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.Aes256CbcHmacSha512];
            options.TokenValidationParameters.AlgorithmValidator = NativeClientStateAlgorithms.Validate;
            options.TokenValidationParameters.ValidIssuers = [profile.ClientUri.AbsoluteUri];
        });
        client.AddRegistration(Registration(profile, credentials));
        StateValidation(client);
        StateExpiration(client);
        Handlers(client);
        Transport(client);
        NativeClientTransportOwnership.Add(client);
    }

    /// <summary>Retains native filters/order and cryptography while correcting only its pre-registration state-key refresh lookup.</summary>
    private static void StateValidation(OpenIddictClientBuilder client)
    {
        var validation = OpenIddictClientHandlers.Protection.ValidateIdentityModelToken.Descriptor;
        client.RemoveEventHandler(validation);
        client.Services.AddSingleton<OpenIddictClientHandlers.Protection.ValidateIdentityModelToken>();
        client.AddEventHandler(OpenIddictClientHandlerDescriptor.CreateBuilder<OpenIddictClientEvents.ValidateTokenContext>()
            .Import(validation).UseSingletonHandler<NativeClientStateValidation>().SetType(OpenIddictClientHandlerType.Custom).Build());
    }

    /// <summary>Defers only the fixed native callback state's expiration denial until its durable consumption; all other native lifetime handling stays intact.</summary>
    private static void StateExpiration(OpenIddictClientBuilder client)
    {
        var expiration = OpenIddictClientHandlers.Protection.ValidateExpirationDate.Descriptor;
        client.RemoveEventHandler(expiration);
        client.Services.AddSingleton<OpenIddictClientHandlers.Protection.ValidateExpirationDate>();
        client.AddEventHandler(OpenIddictClientHandlerDescriptor.CreateBuilder<OpenIddictClientEvents.ValidateTokenContext>()
            .Import(expiration).UseSingletonHandler<NativeClientStateExpiration>().SetType(OpenIddictClientHandlerType.Custom).Build());
    }

    /// <summary>Places profile guards after native parsing and before protected output or optional transport.</summary>
    private static void Handlers(OpenIddictClientBuilder client)
    {
        client.AddEventHandler<OpenIddictClientEvents.ProcessChallengeContext>(handler => handler.UseSingletonHandler<NativeBrowserState>()
            .SetOrder(OpenIddictClientHandlers.PrepareLoginStateTokenPrincipal.Descriptor.Order + PolicyOrderOffset));
        client.AddEventHandler<OpenIddictClientEvents.ProcessChallengeContext>(handler => handler.UseSingletonHandler<NativeClientChallenge>()
            .SetOrder(OpenIddictClientHandlers.AttachResponseMode.Descriptor.Order + PolicyOrderOffset));
        client.AddEventHandler<OpenIddictClientEvents.ProcessChallengeContext>(handler => handler.UseSingletonHandler<NativeClientParBoundary>()
            .SetOrder(OpenIddictClientHandlers.SendPushedAuthorizationRequest.Descriptor.Order - PolicyOrderOffset));
        client.AddEventHandler<OpenIddictClientEvents.ProcessChallengeContext>(handler => handler.UseSingletonHandler<NativeClientParBoundary>()
            .SetOrder(OpenIddictClientHandlers.SendPushedAuthorizationRequest.Descriptor.Order + PolicyOrderOffset));
        client.AddEventHandler<OpenIddictClientEvents.HandleConfigurationResponseContext>(handler => handler.UseSingletonHandler<NativeClientMetadata>()
            .SetOrder(int.MaxValue - PolicyOrderOffset));
        client.AddEventHandler<OpenIddictClientEvents.GenerateTokenContext>(handler => handler.UseSingletonHandler<NativeClientKeys>()
            .SetOrder(OpenIddictClientHandlers.Protection.AttachSecurityCredentials.Descriptor.Order + PolicyOrderOffset));
        client.AddEventHandler<OpenIddictClientEvents.ProcessAuthenticationContext>(handler => handler.UseSingletonHandler<NativeClientUserInfo>()
            .SetOrder(OpenIddictClientHandlers.EvaluateUserInfoRequest.Descriptor.Order - PolicyOrderOffset));
        client.AddEventHandler<OpenIddictClientEvents.ProcessAuthenticationContext>(handler => handler.UseSingletonHandler<NativeClientUserInfoFence>()
            .SetOrder(OpenIddictClientHandlers.SendUserInfoRequest.Descriptor.Order - PolicyOrderOffset));
        client.AddEventHandler<OpenIddictClientEvents.ValidateTokenContext>(handler => handler.UseSingletonHandler<NativeClientValidationParameters>()
            .SetOrder(OpenIddictClientHandlers.Protection.ResolveTokenValidationParameters.Descriptor.Order + PolicyOrderOffset));
        client.AddEventHandler<OpenIddictClientEvents.ValidateTokenContext>(handler => handler.UseSingletonHandler<NativeClientTokenTime>()
            .SetOrder(OpenIddictClientHandlers.Protection.ValidateExpirationDate.Descriptor.Order + PolicyOrderOffset));
        client.AddEventHandler<OpenIddictClientEvents.ProcessAuthenticationContext>(handler => handler.UseSingletonHandler<NativeClientAuthenticationCompletion>()
            .SetOrder(OpenIddictClientHandlers.PopulateMergedPrincipal.Descriptor.Order - PolicyOrderOffset));
        client.AddEventHandler<OpenIddictClientEvents.ProcessAuthenticationContext>(handler => handler.UseSingletonHandler<NativeBrowserIdentity>()
            .SetOrder(OpenIddictClientHandlers.PopulateMergedPrincipal.Descriptor.Order - BrowserIdentityOrderOffset));
    }

    private const int BrowserIdentityOrderOffset = 250;

    /// <summary>Creates the sole registration with no shared secret, alternative grant, scope or authentication method.</summary>
    private static OpenIddictClientRegistration Registration(NativeClientProfile profile, NativeClientCredentials credentials)
    {
        var registration = new OpenIddictClientRegistration
        {
            RegistrationId = AdministratorProtocolIdentity.ClientId,
            ClientId = AdministratorProtocolIdentity.ClientId,
            Issuer = profile.Issuer,
            RedirectUri = profile.Callback,
            ConfigurationEndpoint = profile.ConfigurationEndpoint
        };
        registration.GrantTypes.Add(OpenIddictConstants.GrantTypes.AuthorizationCode);
        registration.ResponseTypes.Add(OpenIddictConstants.ResponseTypes.Code);
        registration.ResponseModes.Add(OpenIddictConstants.ResponseModes.Query);
        registration.CodeChallengeMethods.Add(OpenIddictConstants.CodeChallengeMethods.Sha256);
        registration.ClientAuthenticationMethods.Add(OpenIddictConstants.ClientAuthenticationMethods.PrivateKeyJwt);
        registration.Scopes.UnionWith([OpenIddictConstants.Scopes.OpenId, AdministratorProtocolIdentity.Scope]);
        registration.SigningCredentials.Add(new(new X509SecurityKey(credentials.Assertion) { KeyId = profile.Registration.Key.KeyId }, SecurityAlgorithms.RsaSha256));
        registration.TokenValidationParameters.ValidAlgorithms = [SecurityAlgorithms.RsaSha256];
        registration.TokenValidationParameters.ValidIssuers = [profile.Issuer.AbsoluteUri];
        return registration;
    }

    /// <summary>Removes both native retry mechanisms, bounds each call and sets the distinct test-owned query-callback cookie policy.</summary>
    private static void Transport(OpenIddictClientBuilder client)
    {
        client.UseSystemNetHttp(http =>
        {
            http.Configure(options => { options.HttpErrorPolicy = null; options.HttpResiliencePipeline = null; });
            http.ConfigureHttpClient(value => value.Timeout = TimeSpan.FromSeconds(BackchannelSeconds));
            http.ConfigureHttpClientHandler(value => value.AllowAutoRedirect = false);
        });
        client.UseAspNetCore().Configure(options =>
        {
            options.CookieBuilder.Name = CorrelationPrefix;
            options.CookieBuilder.Path = CookiePath;
            options.CookieBuilder.Domain = null;
            options.CookieBuilder.SecurePolicy = CookieSecurePolicy.Always;
            options.CookieBuilder.HttpOnly = true;
            options.CookieBuilder.SameSite = SameSiteMode.Lax;
        });
    }
}
