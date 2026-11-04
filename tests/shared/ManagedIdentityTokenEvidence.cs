using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Testing;

/// <summary>Keeps verified identity and public keys in memory without diagnostic value serialization or private issuer material.</summary>
internal sealed class ManagedIdentityTokenEvidence(ClaimsPrincipal principal, JsonWebKeySet signingKeys)
{
    internal ClaimsPrincipal Principal => principal;
    internal JsonWebKeySet SigningKeys => signingKeys;
}
