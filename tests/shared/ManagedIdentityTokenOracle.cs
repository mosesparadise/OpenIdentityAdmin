using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Testing;

/// <summary>Checks actual managed ID tokens using only the issuer's public discovery and signing keys.</summary>
internal static class ManagedIdentityTokenOracle
{
    private const string DiscoveryPath = "/.well-known/openid-configuration";
    private const string JwksUri = "jwks_uri";
    private const string IdentityType = "JWT";
    private const string IdentityField = "id_token";
    private const string AccessField = "access_token";
    private const string Subject = "sub";
    private const string Nonce = "nonce";
    private const string AccessHash = "at_hash";
    private const string AuthorizedParty = "azp";
    private const int HalfDigestBytes = 16;
    private static readonly string[] IdentityClaims = ["iss", Subject, "aud", AuthorizedParty, "iat", "exp", "nbf", "jti", Nonce, AccessHash, "oi_au_id", "oi_tkn_id"];

    /// <summary>Requires the original client, subject, nonce and access-token hash after native signature, issuer, audience, type and lifetime validation.</summary>
    internal static async Task<ManagedIdentityTokenEvidence> ValidateAsync(HttpClient transport, Uri issuer, string clientId, string nonce, string subject, JsonElement body)
    {
        var keys = await ReadKeysAsync(transport, issuer);
        var parameters = ManagedTokenValidation.Parameters(issuer, keys, IdentityType);
        parameters.ValidAudience = clientId;
        var principal = await ManagedTokenValidation.ValidateAsync(body.GetProperty(IdentityField).GetString()!, parameters, IdentityClaims);
        var access = body.GetProperty(AccessField).GetString()!;
        var expectedHash = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(access)).AsSpan(0, HalfDigestBytes).ToArray());
        Assert.True(principal.GetClaim(Subject) == subject && principal.GetClaim(Nonce) == nonce && principal.GetClaim(AccessHash) == expectedHash);
        Assert.True(principal.GetClaim(AuthorizedParty) == clientId);
        return new(principal, keys);
    }

    /// <summary>Reads the trusted issuer's public discovery and same-authority JWKS without owning the supplied HTTPS transport.</summary>
    private static async Task<JsonWebKeySet> ReadKeysAsync(HttpClient transport, Uri issuer)
    {
        using var discovery = JsonDocument.Parse(await transport.GetStringAsync(new Uri(issuer, DiscoveryPath), TestContext.Current.CancellationToken));
        var uri = new Uri(discovery.RootElement.GetProperty(JwksUri).GetString()!);
        Assert.True(uri.GetLeftPart(UriPartial.Authority) == issuer.GetLeftPart(UriPartial.Authority));
        var keys = new JsonWebKeySet(await transport.GetStringAsync(uri, TestContext.Current.CancellationToken));
        Assert.All(keys.Keys, key => Assert.True(string.IsNullOrEmpty(key.D) && string.IsNullOrEmpty(key.K)));
        return keys;
    }
}
