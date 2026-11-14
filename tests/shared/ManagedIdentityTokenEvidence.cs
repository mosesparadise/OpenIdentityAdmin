using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Testing;

/// <summary>Keeps verified identity and public keys in memory without diagnostic value serialization or private issuer material.</summary>
internal sealed class ManagedIdentityTokenEvidence(ClaimsPrincipal principal, JsonWebKeySet signingKeys)
{
    internal ClaimsPrincipal Principal => principal;
    internal JsonWebKeySet SigningKeys => signingKeys;
}

/// <summary>Explicitly opts new consent scenarios into exact extended expectations; every existing caller retains the strict OpenID-only oracle.</summary>
internal sealed record ManagedTokenProjection(string Scope, string? Resource = null, string? Email = null, bool? EmailVerified = null);
