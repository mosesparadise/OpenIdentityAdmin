using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Signs synthetic raw JSON and runs real signature validation before the product shape guard; it is not a server-pipeline fixture.</summary>
public sealed class AssertionSigningFixture : IDisposable
{
    internal const string KeyId = "assertion-test-key";
    internal const string Issuer = "https://assertion.example.test/";
    internal const string Payload = "{\"iss\":\"identity-admin-bff\",\"sub\":\"identity-admin-bff\",\"aud\":\"https://assertion.example.test/\",\"iat\":100,\"exp\":160,\"jti\":\"assertion-proof-01\"}";
    internal const string Header = "{\"alg\":\"RS256\",\"typ\":\"client-authentication+jwt\",\"kid\":\"assertion-test-key\"}";
    private const int KeyBits = 3072;
    private const string Separator = ".";
    private readonly RSA rsa = RSA.Create(KeyBits);

    /// <summary>Validates only the actual signature here so product scalar/profile checks remain decisive independent controls.</summary>
    internal async Task<OpenIddictServerEvents.ValidateTokenContext> ContextAsync(string payload = Payload, string header = Header, string validationKeyId = KeyId)
    {
        var data = Base64UrlEncoder.Encode(header) + Separator + Base64UrlEncoder.Encode(payload);
        byte[] signature;
        lock (rsa)
        {
            signature = rsa.SignData(Encoding.ASCII.GetBytes(data), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        var compact = data + Separator + Base64UrlEncoder.Encode(signature);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(compact, new TokenValidationParameters
        {
            IssuerSigningKey = new RsaSecurityKey(rsa) { KeyId = validationKeyId },
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            RequireExpirationTime = false
        });
        return new OpenIddictServerEvents.ValidateTokenContext(new OpenIddictServerTransaction { Logger = NullLogger.Instance })
        {
            Token = compact,
            TokenValidationResult = result,
            Principal = result.IsValid ? new ClaimsPrincipal(result.ClaimsIdentity).SetTokenType(OpenIddictConstants.TokenTypeIdentifiers.Private.ClientAssertion) : null,
            ValidTokenTypes = { OpenIddictConstants.TokenTypeIdentifiers.Private.ClientAssertion }
        };
    }

    /// <summary>Disposes the test-only key after every signature/shape control has completed.</summary>
    public void Dispose() => rsa.Dispose();
}
