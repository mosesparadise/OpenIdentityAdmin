using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Captures the single administrator client identity, canonical destinations and public assertion-key binding.</summary>
internal sealed class NativeClientProfile
{
    internal const string AuthorizePath = "/connect/authorize";
    internal const string TokenPath = "/connect/token";
    internal const string ParPath = "/connect/par";
    internal const string KeysPath = "/connect/jwks";
    internal const string ConfigurationPath = ".well-known/openid-configuration";
    private const string PathSeparator = "/";
    private const string RsaType = "RSA";
    private const string SignatureUse = "sig";
    internal Uri Issuer
    {
        get;
    }
    internal Uri ClientUri
    {
        get;
    }
    internal AdministratorRegistrationPolicy Registration
    {
        get;
    }
    internal Uri Callback => new(Registration.RedirectUri);
    internal Uri AuthorizationEndpoint => new(Issuer, AuthorizePath);
    internal Uri TokenEndpoint => new(Issuer, TokenPath);
    internal Uri ParEndpoint => new(Issuer, ParPath);
    internal Uri KeysEndpoint => new(Issuer, KeysPath);
    internal Uri ConfigurationEndpoint => new(Issuer.AbsoluteUri.TrimEnd('/') + PathSeparator + ConfigurationPath);

    /// <summary>Retains only validated immutable URI and public-key policy; private material remains in its separate lifetime owner.</summary>
    private NativeClientProfile(Uri issuer, Uri client, AdministratorRegistrationPolicy registration)
    {
        Issuer = issuer;
        ClientUri = client;
        Registration = registration;
    }

    /// <summary>Requires exact canonical HTTPS identities, an exact same-origin callback and an explicit public key identifier.</summary>
    internal static NativeClientProfile Create(string issuer, string clientUri, string callback, string keyId, NativeClientCredentials credentials)
    {
        try
        {
            var server = IssuerUri.Parse(issuer);
            var client = IssuerUri.Parse(clientUri);
            var registration = AdministratorRegistrationPolicy.Create(callback, PublicKey(credentials.Assertion, keyId));
            if (server.AbsoluteUri != issuer || client.AbsoluteUri != clientUri || registration is null ||
                new Uri(callback).GetLeftPart(UriPartial.Authority) != client.GetLeftPart(UriPartial.Authority))
            {
                throw new ClientCredentialFailure();
            }
            return new(server, client, registration);
        }
        catch (Exception error) when (error is ArgumentException or FormatException or CryptographicException) { throw new ClientCredentialFailure(); }
    }

    /// <summary>Exports only the RSA public integers into the same strict registration policy used by explicit initialization.</summary>
    private static AdministratorPublicKey PublicKey(X509Certificate2 certificate, string identifier)
    {
        using var rsa = certificate.GetRSAPublicKey() ?? throw new ClientCredentialFailure();
        var key = rsa.ExportParameters(includePrivateParameters: false);
        return AdministratorPublicKey.Parse(JsonSerializer.Serialize(new
        {
            kty = RsaType,
            kid = identifier,
            alg = SecurityAlgorithms.RsaSha256,
            use = SignatureUse,
            n = Base64UrlEncoder.Encode(key.Modulus!),
            e = Base64UrlEncoder.Encode(key.Exponent!)
        })) ?? throw new ClientCredentialFailure();
    }
}
