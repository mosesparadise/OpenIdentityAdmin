using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Testing;

/// <summary>Builds independent confidential OpenID-only authorization intent for native and real browser tests.</summary>
internal static class ManagedAuthorizationRequest
{
    private const int RandomBytes = 32;

    /// <summary>Retains independently generated state, nonce and S256 proof without replacing issuer protocol validation.</summary>
    internal static Dictionary<string, string> Initial(string verifier, string client, string callback) => new(StringComparer.Ordinal)
    {
        [OpenIddictConstants.Parameters.ClientId] = client,
        [OpenIddictConstants.Parameters.RedirectUri] = callback,
        [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
        [OpenIddictConstants.Parameters.Scope] = OpenIddictConstants.Scopes.OpenId,
        [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256,
        [OpenIddictConstants.Parameters.CodeChallenge] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),
        [OpenIddictConstants.Parameters.State] = Random(),
        [OpenIddictConstants.Parameters.Nonce] = Random()
    };

    /// <summary>Creates one bounded synthetic correlation or verifier value retained only in test memory.</summary>
    internal static string Random() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(RandomBytes));
}
