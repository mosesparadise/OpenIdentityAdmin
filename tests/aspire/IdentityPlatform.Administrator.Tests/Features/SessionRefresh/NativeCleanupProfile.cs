using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Defines an isolated characterization profile, never registered by the product issuer or BFF.</summary>
internal static class NativeCleanupProfile
{
    internal const string Issuer = "https://cleanup-characterization.example.test/";
    internal const string Callback = "https://cleanup-client.example.test/callback";
    internal const string Client = "cleanup-characterization-client";
    internal const string Authorize = "/connect/authorize";
    internal const string Token = "/connect/token";
    internal const string Revoke = "/connect/revoke";
    internal const string SelectorType = "test_cleanup_selector";
    internal const string Intent = "test_login_intent";
    internal const string Nonce = "test_cleanup_nonce";
    internal const string Confirmation = "test_cleanup_confirmed";
    internal const string Subject = "cleanup-characterization-subject";
    internal const string Scope = "cleanup.characterization";
    internal const int OrderOffset = 500;
    internal const int RandomBytes = 32;
    private const int EncodedRandomLength = 43;
    private const string ReferenceSeparator = ":";
    private const string ReferenceDomain = "native-cleanup-characterization:v1:";

    /// <summary>Uses a client-qualified noncredential reference; the native manager hashes it again for indexed storage.</summary>
    internal static string Reference(string client, string intent) => ReferenceDomain + client + ReferenceSeparator + intent;

    /// <summary>Creates a bounded random test selector or nonce without deriving either from an issued credential.</summary>
    internal static string Random() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(RandomBytes));

    /// <summary>Accepts one canonical fixed-size base64url value before constructing a database selector or response field.</summary>
    internal static bool IsRandom(string? value)
    {
        if (value is null || value.Length != EncodedRandomLength || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            return false;
        }
        var bytes = Base64UrlEncoder.DecodeBytes(value);
        return bytes.Length == RandomBytes && Base64UrlEncoder.Encode(bytes) == value;
    }

    /// <summary>Computes the actual S256 challenge supplied to the installed native PKCE handlers.</summary>
    internal static string Challenge(string verifier) => Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    /// <summary>Preserves the complete native crypto/HTTP graph and changes only the explicit test selector profile.</summary>
    internal static void Configure(IServiceCollection services, SecurityKey signing, SecurityKey encryption)
    {
        services.AddOpenIddict().AddServer(server =>
        {
            server.SetIssuer(Issuer).SetAuthorizationEndpointUris(Authorize).SetTokenEndpointUris(Token).SetRevocationEndpointUris(Revoke);
            server.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow().RequireProofKeyForCodeExchange();
            server.RegisterScopes(Scope).AddSigningKey(signing).AddEncryptionKey(encryption);
            server.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableTokenEndpointPassthrough();
            server.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler
                .SetOrder(OpenIddictServerHandlers.AttachAuthorization.Descriptor.Order + OrderOffset)
                .UseScopedHandler<NativeCleanupBinding>());
            NativeCleanupAdapter.Configure(server);
        });
        services.AddOptions<OpenIddictServerOptions>().Configure<TimeProvider>((options, clock) => options.TimeProvider = clock);
    }
}
