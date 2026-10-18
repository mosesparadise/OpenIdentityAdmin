using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Separates encrypted state's outer content-encryption check from its inner asymmetric signature check.</summary>
internal static class NativeClientStateAlgorithms
{
    /// <summary>IdentityModel applies the same algorithm callback to JWE content and JWS signatures; permit only each role's native algorithm.</summary>
    internal static bool Validate(string algorithm, SecurityKey key, SecurityToken token, TokenValidationParameters parameters) => token switch
    {
        JsonWebToken { IsEncrypted: true } encrypted => algorithm == SecurityAlgorithms.Aes256CbcHmacSha512 &&
            encrypted.Enc == SecurityAlgorithms.Aes256CbcHmacSha512 && encrypted.Alg == SecurityAlgorithms.RsaOAEP,
        JsonWebToken { IsEncrypted: false } => algorithm == SecurityAlgorithms.RsaSha256 && key is X509SecurityKey or RsaSecurityKey,
        _ => false
    };
}
