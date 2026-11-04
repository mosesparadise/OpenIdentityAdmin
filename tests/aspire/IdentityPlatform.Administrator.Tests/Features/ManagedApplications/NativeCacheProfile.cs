using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Defines only an isolated native caching characterization server; no production profile or human proof is enabled.</summary>
internal static class NativeCacheProfile
{
    internal const string Issuer = "https://cache-characterization.example.test/";
    internal const string Callback = "https://cache-client.example.test/callback";
    internal const string Client = "cache-characterization-client";
    internal const string SecondClient = "cache-second-client";
    internal const string SecondCallback = "https://cache-second.example.test/callback";
    internal const string Authorize = "/connect/authorize";
    internal const string Token = "/connect/token";
    internal const string Subject = "cache-characterization-subject";
    internal const int LifetimeSeconds = 300;
    private const int RandomBytes = 32;

    /// <summary>Creates independent synthetic secrets, state, nonce and verifier without retaining them in diagnostics.</summary>
    internal static string Random() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(RandomBytes));

    /// <summary>Computes the actual challenge verified by native PKCE processing.</summary>
    internal static string Challenge(string verifier) => Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    /// <summary>Preserves installed native handlers; additive observers inject only explicit faults after actual redemption.</summary>
    internal static void Configure(IServiceCollection services, SecurityKey signing, SecurityKey encryption)
    {
        services.AddOpenIddict().AddServer(server =>
        {
            server.SetIssuer(Issuer).SetAuthorizationEndpointUris(Authorize).SetTokenEndpointUris(Token);
            server.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
            server.AddSigningKey(signing).AddEncryptionKey(encryption);
            server.EnableAuthorizationRequestCaching();
            server.Configure(options => options.RequestTokenLifetime = TimeSpan.FromSeconds(LifetimeSeconds));
            server.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableTokenEndpointPassthrough();
            server.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler
                .SetOrder(OpenIddictServerHandlers.ValidateSignInDemand.Descriptor.Order - 1)
                .UseInlineHandler(NativeCacheProbe.Observe));
            server.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler
                .SetOrder(OpenIddictServerHandlers.RedeemTokenEntry.Descriptor.Order + 1)
                .UseScopedHandler<NativeCacheProbe>());
        });
    }
}
