using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Testing;

/// <summary>Shares the existing strict native token validation and public claim cardinality oracle.</summary>
internal static class ManagedTokenValidation
{
    private const string ProjectionBypass = "managed-email-consent-bypass";
    /// <summary>Requires native signed credentials with exact issuer/type and strict current expiry.</summary>
    internal static TokenValidationParameters Parameters(Uri issuer, JsonWebKeySet keys, string type) => new()
    {
        IssuerSigningKeys = keys.GetSigningKeys(),
        ValidIssuer = issuer.AbsoluteUri,
        ValidTypes = [type],
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    /// <summary>Rejects unapproved public claims and duplicate principal claims without echoing token contents.</summary>
    internal static async Task<ClaimsPrincipal> ValidateAsync(string token, TokenValidationParameters parameters, string[] allowed)
    {
        var result = await new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(token, parameters);
        Assert.True(result.IsValid, result.Exception?.GetType().FullName);
        var principal = new ClaimsPrincipal(result.ClaimsIdentity);
        Assert.True(principal.Claims.All(claim => allowed.Contains(claim.Type, StringComparer.Ordinal)), ProjectionBypass);
        Assert.True(principal.Claims.GroupBy(claim => claim.Type).All(group => group.Count() == 1));
        return principal;
    }
}
