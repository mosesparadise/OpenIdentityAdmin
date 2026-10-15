using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using IdentityPlatform.AuthServer.Features.Issuer.DataProtection;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Protection;

namespace IdentityPlatform.AuthServer.Features.Issuer.Credentials;

/// <summary>Composition-root owner. Dispose after the host and all certificate consumers.</summary>
/// <example><code>using var material = IssuerCredentials.Load(configuration, DateTimeOffset.UtcNow); // Dispose after consumers.</code></example>
public sealed class IssuerCredentials : IDisposable
{
    private readonly List<X509Certificate2> certificates = [];
    private readonly List<X509Certificate2> wrappingDecryptors = [];
    private enum Validity
    {
        Active, Staged, Previous, RetainedWrapper
    }
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
    public IssuerKeySet SigningKeys { get; private set; } = null!;
    public IssuerKeySet EncryptionKeys { get; private set; } = null!;
    public X509Certificate2 Signing => SigningKeys.Active;
    public X509Certificate2 Encryption => EncryptionKeys.Active;
    public X509Certificate2 Wrapping { get; private set; } = null!;
    public IEnumerable<X509Certificate2> WrappingDecryptors => wrappingDecryptors.AsReadOnly();

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
            material.SigningKeys = material.LoadKeySet(protection.GetSection("Signing"), X509KeyUsageFlags.DigitalSignature, now, fingerprints);
            material.EncryptionKeys = material.LoadKeySet(protection.GetSection("Encryption"), X509KeyUsageFlags.KeyEncipherment, now, fingerprints);
            material.Wrapping = material.LoadCertificate(protection.GetSection("DataProtection:Current"), X509KeyUsageFlags.KeyEncipherment, now, Validity.Active, fingerprints);
            material.wrappingDecryptors.Add(material.Wrapping);
            var previous = protection.GetSection("DataProtection:Previous").GetChildren().ToArray();
            if (previous.Length > 4)
            {
                throw new IssuerProtectionException();
            }

            foreach (var entry in previous)
            {
                material.wrappingDecryptors.Add(material.LoadCertificate(entry, X509KeyUsageFlags.KeyEncipherment, now, Validity.RetainedWrapper, fingerprints));
            }

            return material;
        }
        catch (Exception exception) when (IssuerProtectionRegistration.IsConfigurationFailure(exception))
        {
            material?.Dispose();
            throw new IssuerProtectionException();
        }
    }

    private IssuerKeySet LoadKeySet(IConfigurationSection section, X509KeyUsageFlags usage,
        DateTimeOffset now, HashSet<string> fingerprints)
    {
        RequireFields(section, "Path", "Password", "Staged", "Previous");
        var active = LoadCertificate(section, usage, now, Validity.Active, fingerprints);
        var stagedSection = section.GetSection("Staged");
        X509Certificate2? staged = null;
        if (stagedSection.Exists())
        {
            RequireFields(stagedSection, "Path", "Password");
            staged = LoadCertificate(stagedSection, usage, now, Validity.Staged, fingerprints);
        }
        var previousSection = section.GetSection("Previous");
        var previous = previousSection.GetChildren().ToArray();
        if (previousSection.Value is not null || previous.Length > 4)
        {
            throw new IssuerProtectionException();
        }
        var retained = new List<X509Certificate2>();
        for (var index = 0; index < previous.Length; index++)
        {
            if (previous[index].Key != index.ToString(System.Globalization.CultureInfo.InvariantCulture))
            {
                throw new IssuerProtectionException();
            }
            RequireFields(previous[index], "Path", "Password");
            retained.Add(LoadCertificate(previous[index], usage, now, Validity.Previous, fingerprints));
        }
        return new IssuerKeySet(active, staged, [.. retained]);
    }

    private static void RequireFields(IConfigurationSection section, params string[] allowed)
    {
        if (section.Value is not null || section.GetChildren().Any(child =>
            !allowed.Contains(child.Key, StringComparer.OrdinalIgnoreCase) ||
            (child.Key.Equals("Path", StringComparison.OrdinalIgnoreCase) || child.Key.Equals("Password", StringComparison.OrdinalIgnoreCase)) && child.GetChildren().Any()))
        {
            throw new IssuerProtectionException();
        }
    }

    /// <summary>Applies issuer role/validity and uniqueness policy to bounded encrypted certificate material owned by this object.</summary>
    private X509Certificate2 LoadCertificate(IConfigurationSection section, X509KeyUsageFlags usage,
        DateTimeOffset now, Validity validity, HashSet<string> fingerprints)
    {
        const string PathSetting = "Path";
        const string PasswordSetting = "Password";
        const int MinimumRsaBits = 3072;
        const int ProbeBytes = 32;
        var certificate = ProtectedCertificate.Load(section[PathSetting], section[PasswordSetting]);
        certificates.Add(certificate); // Include partial loads in owner cleanup before role validation.
        using var key = certificate.GetRSAPrivateKey();
        using var publicKey = certificate.GetRSAPublicKey();
        var keyUsage = certificate.Extensions.OfType<X509KeyUsageExtension>().SingleOrDefault();
        if (key is null || publicKey is null || key.KeySize < MinimumRsaBits ||
            keyUsage is null || (keyUsage.KeyUsages & usage) != usage ||
            (validity is Validity.Active or Validity.Previous && now < certificate.NotBefore.ToUniversalTime()) ||
            (validity is Validity.Active or Validity.Staged && now >= certificate.NotAfter.ToUniversalTime()) ||
            !fingerprints.Add(Convert.ToHexString(SHA256.HashData(publicKey.ExportSubjectPublicKeyInfo()))))
        {
            throw new IssuerProtectionException();
        }

        // HasPrivateKey is metadata; a real operation proves that the imported private key is usable.
        var probe = RandomNumberGenerator.GetBytes(ProbeBytes);
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
        return certificate;
    }

    public void Dispose()
    {
        foreach (var certificate in certificates)
        {
            certificate.Dispose();
        }
    }
}
