using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Owns disposable signing material shared by two native test instances; only its public key enters registration.</summary>
internal sealed class NativeAssertionMaterial : IDisposable
{
    internal const string Issuer = "https://native-assertion.example.test/";
    private const string Callback = "https://native-assertion.example.test/callback";
    private const string KeyId = "native-assertion-key";
    private const int KeyBits = 3072;
    private const string RsaType = "RSA";
    private const string SignatureUse = "sig";
    private const int ProofSeconds = 60;
    private readonly RSA rsa = RSA.Create(KeyBits);
    internal AdministratorRegistrationPolicy Policy
    {
        get;
    }

    /// <summary>Creates exact public test configuration before constructing either service provider.</summary>
    internal NativeAssertionMaterial()
    {
        var parameters = rsa.ExportParameters(false);
        var json = JsonSerializer.Serialize(new
        {
            kty = RsaType,
            kid = KeyId,
            alg = SecurityAlgorithms.RsaSha256,
            use = SignatureUse,
            n = Base64UrlEncoder.Encode(parameters.Modulus),
            e = Base64UrlEncoder.Encode(parameters.Exponent)
        });
        Policy = AdministratorRegistrationPolicy.Create(Callback, AdministratorPublicKey.Parse(json)!)!;
    }

    /// <summary>Signs a fresh intentional proof with real RSA; native server validation must verify it against persisted public registration.</summary>
    internal string Sign(long now, string? identifier = null, int lifetime = ProofSeconds)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = AdministratorProtocolIdentity.ClientId,
            Audience = Issuer,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = AdministratorProtocolIdentity.ClientId,
                [JwtRegisteredClaimNames.Jti] = identifier ?? Guid.NewGuid().ToString()
            },
            IssuedAt = DateTimeOffset.FromUnixTimeSeconds(now).UtcDateTime,
            Expires = DateTimeOffset.FromUnixTimeSeconds(now + lifetime).UtcDateTime,
            TokenType = OpenIddictConstants.JsonWebTokenTypes.ClientAuthentication,
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = KeyId }, SecurityAlgorithms.RsaSha256)
        };
        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }

    /// <summary>Releases private test material only after both native instances finish.</summary>
    public void Dispose() => rsa.Dispose();
}
