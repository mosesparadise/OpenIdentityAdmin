using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Independently verifies the recipient's ID-token signature and bindings; this is not native client interoperability acceptance.</summary>
internal static class NativeIdentityTokenOracle
{
    private const int HalfDigestBytes = 16;

    /// <summary>Uses the actual durable issuer validation keys and fixed recipient without bypassing server-side ID-token rejection.</summary>
    internal static async Task<ClaimsPrincipal> ValidateAsync(AdministratorProtocolScenario scenario, string identity, string access)
    {
        var credentials = scenario.Services.GetRequiredService<IssuerCredentials>();
        var result = await new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(identity, new TokenValidationParameters
        {
            IssuerSigningKeys = credentials.SigningKeys.All.Select(certificate => new X509SecurityKey(certificate)),
            ValidIssuer = scenario.Issuer.AbsoluteUri,
            ValidAudience = AdministratorProtocolIdentity.ClientId,
            ValidTypes = [OpenIddictConstants.JsonWebTokenTypes.GenericJsonWebToken],
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        });
        Assert.True(result.IsValid, result.Exception?.GetType().FullName);
        var principal = new ClaimsPrincipal(result.ClaimsIdentity);
        var expectedHash = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(access)).AsSpan(0, HalfDigestBytes).ToArray());
        Assert.True(principal.GetClaim(OpenIddictConstants.Claims.AccessTokenHash) == expectedHash);
        Assert.True(principal.GetClaim(OpenIddictConstants.Claims.Nonce) == NativeAccountFlow.Nonce);
        return principal;
    }
}
