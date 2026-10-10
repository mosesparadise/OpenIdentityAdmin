using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using IdentityPlatform.AuthServer.Features.Issuer.DataProtection;
using IdentityPlatform.Core.Features.Configuration;

namespace IdentityPlatform.AuthServer.Features.Issuer.Credentials;

/// <summary>Composition-root owner. Dispose after the host and all certificate consumers.</summary>
public sealed class IssuerCredentials : IDisposable
{
    private readonly List<X509Certificate2> certificates = [];
    private IssuerCredentials(Uri issuer, string keyRingPath)
    {
        Issuer = issuer;
        KeyRingPath = keyRingPath;
    }

    public Uri Issuer
    {
        get;
    }
    public string KeyRingPath
    {
        get;
    }
    public X509Certificate2 Signing => certificates[0];
    public X509Certificate2 Encryption => certificates[1];
    public X509Certificate2 Wrapping => certificates[2];
    public IEnumerable<X509Certificate2> WrappingDecryptors => certificates.Skip(2);

    public static IssuerCredentials Load(IConfiguration configuration, DateTimeOffset now)
    {
        IssuerCredentials? material = null;
        try
        {
            var identity = configuration.GetSection("Identity");
            material = new IssuerCredentials(IssuerUri.Parse(identity["Issuer"]),
                PrivateStorage.RequireDirectory(identity["Protection:DataProtection:KeyRingPath"]));
            var protection = identity.GetSection("Protection");
            var fingerprints = new HashSet<string>(StringComparer.Ordinal);
            material.LoadCertificate(protection.GetSection("Signing"), X509KeyUsageFlags.DigitalSignature, now, active: true, fingerprints);
            material.LoadCertificate(protection.GetSection("Encryption"), X509KeyUsageFlags.KeyEncipherment, now, active: true, fingerprints);
            material.LoadCertificate(protection.GetSection("DataProtection:Current"), X509KeyUsageFlags.KeyEncipherment, now, active: true, fingerprints);
            var previous = protection.GetSection("DataProtection:Previous").GetChildren().ToArray();
            if (previous.Length > 4)
            {
                throw new IssuerProtectionException();
            }

            foreach (var entry in previous)
            {
                material.LoadCertificate(entry, X509KeyUsageFlags.KeyEncipherment, now, active: false, fingerprints);
            }

            return material;
        }
        catch (Exception exception) when (IssuerProtectionRegistration.IsConfigurationFailure(exception))
        {
            material?.Dispose();
            throw new IssuerProtectionException();
        }
    }

    private void LoadCertificate(IConfigurationSection section, X509KeyUsageFlags usage,
        DateTimeOffset now, bool active, HashSet<string> fingerprints)
    {
        var path = PrivateStorage.RequireCredentialFile(section["Path"]);
        var password = section["Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new IssuerProtectionException();
        }

        var bytes = PrivateStorage.ReadBoundedFile(path);
        try
        {
            var certificate = X509CertificateLoader.LoadPkcs12(bytes, password, X509KeyStorageFlags.EphemeralKeySet,
                new Pkcs12LoaderLimits { MaxCertificates = 1, MaxKeys = 1 });
            certificates.Add(certificate); // Include partial loads in owner cleanup before validation.
            RequireProtectedPrivateKey(bytes, password);
            using var key = certificate.GetRSAPrivateKey();
            using var publicKey = certificate.GetRSAPublicKey();
            var keyUsage = certificate.Extensions.OfType<X509KeyUsageExtension>().SingleOrDefault();
            if (key is null || publicKey is null || key.KeySize < 3072 ||
                keyUsage is null || (keyUsage.KeyUsages & usage) != usage ||
                (active && (now < certificate.NotBefore.ToUniversalTime() || now >= certificate.NotAfter.ToUniversalTime())) ||
                !fingerprints.Add(Convert.ToHexString(SHA256.HashData(publicKey.ExportSubjectPublicKeyInfo()))))
            {
                throw new IssuerProtectionException();
            }

            // HasPrivateKey is metadata; a real operation proves that the imported private key is usable.
            var probe = RandomNumberGenerator.GetBytes(32);
            if (usage == X509KeyUsageFlags.DigitalSignature)
            {
                var signature = key.SignData(probe, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                if (!publicKey.VerifyData(probe, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                {
                    throw new IssuerProtectionException();
                }
            }
            else
            {
                var decrypted = key.Decrypt(publicKey.Encrypt(probe, RSAEncryptionPadding.OaepSHA256), RSAEncryptionPadding.OaepSHA256);
                if (!CryptographicOperations.FixedTimeEquals(probe, decrypted))
                {
                    throw new IssuerProtectionException();
                }
            }
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static void RequireProtectedPrivateKey(byte[] bytes, string password)
    {
        // Run only AFTER the bounded native loader has checked PKCS#12 work/certificate/key limits.
        var info = Pkcs12Info.Decode(bytes, out var consumed, skipCopy: true);
        if (consumed != bytes.Length || info.IntegrityMode != Pkcs12IntegrityMode.Password || !info.VerifyMac(password))
        {
            throw new IssuerProtectionException();
        }

        var protectedKeys = 0;
        foreach (var contents in info.AuthenticatedSafe)
        {
            if (contents.ConfidentialityMode == Pkcs12ConfidentialityMode.Password)
            {
                contents.Decrypt(password);
            }
            else if (contents.ConfidentialityMode != Pkcs12ConfidentialityMode.None)
            {
                throw new IssuerProtectionException();
            }

            foreach (var bag in contents.GetBags())
            {
                if (bag is Pkcs12ShroudedKeyBag)
                {
                    protectedKeys++;
                }
                else if (bag is not Pkcs12CertBag)
                {
                    throw new IssuerProtectionException();
                }
            }
        }
        if (protectedKeys != 1)
        {
            throw new IssuerProtectionException();
        }
    }

    public void Dispose()
    {
        foreach (var certificate in certificates)
        {
            certificate.Dispose();
        }
    }
}
