using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;

/// <summary>Applies distinct client key roles, minimum RSA strength, explicit validity and a real private-key operation.</summary>
internal static class ClientCertificatePolicy
{
    private const int ProbeBytes = 32;

    /// <summary>Retained state credentials may be expired for validation/decryption; active credentials must be current.</summary>
    internal static void Require(X509Certificate2 certificate, X509KeyUsageFlags usage, DateTimeOffset now, bool retained, HashSet<string> fingerprints)
    {
        using var privateKey = certificate.GetRSAPrivateKey();
        using var publicKey = certificate.GetRSAPublicKey();
        var declared = certificate.Extensions.OfType<X509KeyUsageExtension>().SingleOrDefault();
        if (privateKey is null || publicKey is null || privateKey.KeySize < AdministratorPublicKey.MinimumBits ||
            declared is null || (declared.KeyUsages & usage) != usage || now < certificate.NotBefore.ToUniversalTime() ||
            (!retained && now >= certificate.NotAfter.ToUniversalTime()) ||
            !fingerprints.Add(Convert.ToHexString(SHA256.HashData(publicKey.ExportSubjectPublicKeyInfo()))))
        {
            throw new ClientCredentialFailure();
        }
        Probe(privateKey, publicKey, usage);
    }

    /// <summary>Requires usable imported private material rather than trusting certificate metadata alone.</summary>
    private static void Probe(RSA privateKey, RSA publicKey, X509KeyUsageFlags usage)
    {
        var probe = RandomNumberGenerator.GetBytes(ProbeBytes);
        try
        {
            var valid = usage == X509KeyUsageFlags.DigitalSignature
                ? publicKey.VerifyData(probe, privateKey.SignData(probe, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
                : CryptographicOperations.FixedTimeEquals(probe, privateKey.Decrypt(publicKey.Encrypt(probe, RSAEncryptionPadding.OaepSHA256), RSAEncryptionPadding.OaepSHA256));
            if (!valid)
            {
                throw new ClientCredentialFailure();
            }
        }
        finally { CryptographicOperations.ZeroMemory(probe); }
    }
}
