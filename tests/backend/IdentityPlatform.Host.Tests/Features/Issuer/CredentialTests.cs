using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Issuer.DataProtection;
using IdentityPlatform.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.Issuer;

public sealed class CredentialTests
{
    [Fact]
    public void Entirely_absent_profile_keeps_health_only_composition()
    {
        var services = new ServiceCollection();
        Assert.Null(services.AddIssuerProtection(new ConfigurationBuilder().Build()));
        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<IssuerCredentials>());
    }

    [Theory]
    [InlineData("Identity:Issuer")]
    [InlineData("Identity:Protection:Signing:Path")]
    [InlineData("Identity:Protection:Signing:Password")]
    [InlineData("Identity:Protection:Encryption:Path")]
    [InlineData("Identity:Protection:Encryption:Password")]
    [InlineData("Identity:Protection:DataProtection:Current:Path")]
    [InlineData("Identity:Protection:DataProtection:Current:Password")]
    [InlineData("Identity:Protection:DataProtection:KeyRingPath")]
    public void Partial_profile_is_rejected(string missing)
    {
        using var fixture = new IssuerMaterialFixture();
        fixture.Settings.Remove(missing);
        AssertSafeFailure(fixture);
    }

    [Theory]
    [InlineData("http://issuer.example.test/")]
    [InlineData("https://secret:private@issuer.example.test/")]
    [InlineData("https://issuer.example.test/?secret=private")]
    [InlineData("https://issuer.example.test/#private")]
    public void Unsafe_issuer_is_rejected(string value)
    {
        using var fixture = new IssuerMaterialFixture();
        fixture.Settings["Identity:Issuer"] = value;
        AssertSafeFailure(fixture);
    }

    [Theory]
    [InlineData("wrong-password")]
    [InlineData("missing-file")]
    [InlineData("invalid-file")]
    [InlineData("oversize-file")]
    [InlineData("public-only")]
    [InlineData("weak-key")]
    [InlineData("wrong-usage")]
    [InlineData("missing-usage")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("elliptic")]
    public void Unusable_active_material_is_rejected(string scenario)
    {
        using var fixture = new IssuerMaterialFixture();
        switch (scenario)
        {
            case "wrong-password":
                fixture.Settings["Identity:Protection:Signing:Password"] = "synthetic-private-sentinel";
                break;
            case "missing-file":
                File.Delete(fixture.Settings["Identity:Protection:Signing:Path"]!);
                break;
            case "invalid-file":
                File.WriteAllText(fixture.Settings["Identity:Protection:Signing:Path"]!, "synthetic-private-sentinel");
                break;
            case "oversize-file":
                File.WriteAllBytes(fixture.Settings["Identity:Protection:Signing:Path"]!, new byte[1_048_577]);
                break;
            case "public-only":
                fixture.AddCertificate("Signing", X509KeyUsageFlags.DigitalSignature, publicOnly: true);
                break;
            case "weak-key":
                fixture.AddCertificate("Signing", X509KeyUsageFlags.DigitalSignature, keySize: 2048);
                break;
            case "wrong-usage":
                fixture.AddCertificate("Signing", X509KeyUsageFlags.KeyEncipherment);
                break;
            case "missing-usage":
                fixture.AddCertificate("Signing", null);
                break;
            case "expired":
                fixture.AddCertificate("Signing", X509KeyUsageFlags.DigitalSignature, notBefore: DateTimeOffset.UtcNow.AddDays(-2), notAfter: DateTimeOffset.UtcNow.AddDays(-1));
                break;
            case "future":
                fixture.AddCertificate("Signing", X509KeyUsageFlags.DigitalSignature, notBefore: DateTimeOffset.UtcNow.AddDays(1));
                break;
            case "elliptic":
                fixture.AddCertificate("Signing", X509KeyUsageFlags.DigitalSignature, elliptic: true);
                break;
        }
        AssertSafeFailure(fixture);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Unencrypted_private_key_bags_are_rejected_even_with_a_password_or_integrity_mac(bool withMac)
    {
        using var fixture = new IssuerMaterialFixture();
        var path = fixture.Settings["Identity:Protection:Signing:Path"]!;
        var password = fixture.Settings["Identity:Protection:Signing:Password"]!;
        using var certificate = X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet);
        using var key = certificate.GetRSAPrivateKey()!;
        var contents = new Pkcs12SafeContents();
        contents.AddCertificate(certificate);
        contents.AddKeyUnencrypted(key);
        var builder = new Pkcs12Builder();
        builder.AddSafeContentsUnencrypted(contents);
        if (withMac)
        {
            builder.SealWithMac(password, HashAlgorithmName.SHA256, 2048);
        }
        else
        {
            builder.SealWithoutIntegrity();
        }

        var bytes = builder.Encode();
        try
        {
            File.WriteAllBytes(path, bytes);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        AssertSafeFailure(fixture);
    }

    [Fact]
    public void Historical_wrapper_count_accepts_four_and_rejects_five_distinct_keys()
    {
        using var fixture = new IssuerMaterialFixture();
        for (var index = 0; index < 4; index++)
        {
            fixture.AddCertificate($"DataProtection:Previous:{index}", X509KeyUsageFlags.KeyEncipherment);
        }

        using (var provider = CreateProvider(fixture))
        {
            provider.VerifyIssuerProtection();
        }

        fixture.AddCertificate("DataProtection:Previous:4", X509KeyUsageFlags.KeyEncipherment);
        AssertSafeFailure(fixture);
    }

    [Fact]
    public void Ambiguous_multiple_private_key_bundle_is_rejected()
    {
        using var fixture = new IssuerMaterialFixture();
        var password = fixture.Settings["Identity:Protection:Signing:Password"]!;
        using var signing = X509CertificateLoader.LoadPkcs12FromFile(fixture.Settings["Identity:Protection:Signing:Path"]!, password, X509KeyStorageFlags.EphemeralKeySet);
        using var encryption = X509CertificateLoader.LoadPkcs12FromFile(fixture.Settings["Identity:Protection:Encryption:Path"]!, fixture.Settings["Identity:Protection:Encryption:Password"]!, X509KeyStorageFlags.EphemeralKeySet);
        var collection = new X509Certificate2Collection(new[] { signing, encryption });
        var bytes = collection.Export(X509ContentType.Pfx, password)!;
        try
        {
            File.WriteAllBytes(fixture.Settings["Identity:Protection:Signing:Path"]!, bytes);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        AssertSafeFailure(fixture);
    }

    [Theory]
    [InlineData("Encryption")]
    [InlineData("DataProtection:Current")]
    [InlineData("DataProtection:Previous:0")]
    public void Reissued_certificates_cannot_reuse_a_signing_key_for_another_purpose(string other)
    {
        using var fixture = new IssuerMaterialFixture();
        using var key = RSA.Create(3072);
        fixture.AddCertificate("Signing", X509KeyUsageFlags.DigitalSignature, sharedKey: key);
        fixture.AddCertificate(other, X509KeyUsageFlags.KeyEncipherment, sharedKey: key);
        AssertSafeFailure(fixture);
    }

    [Fact]
    public void Loaded_external_keys_perform_independently_verified_crypto_and_are_owned_until_provider_disposal()
    {
        using var fixture = new IssuerMaterialFixture();
        var provider = CreateProvider(fixture);
        var material = provider.GetRequiredService<IssuerCredentials>();
        var data = RandomNumberGenerator.GetBytes(32);
        using (var signer = material.Signing.GetRSAPrivateKey()!)
        using (var verifier = material.Signing.GetRSAPublicKey()!)
        using (var encryptor = material.Encryption.GetRSAPublicKey()!)
        using (var decryptor = material.Encryption.GetRSAPrivateKey()!)
        {
            var signature = signer.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            Assert.True(verifier.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            signature[0] ^= 1;
            Assert.False(verifier.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            var encrypted = encryptor.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
            Assert.Equal(data, decryptor.Decrypt(encrypted, RSAEncryptionPadding.OaepSHA256));
            Assert.ThrowsAny<CryptographicException>(() => signer.Decrypt(encrypted, RSAEncryptionPadding.OaepSHA256));
        }
        provider.Dispose();
        Assert.Throws<CryptographicException>(() => material.Signing.GetRSAPrivateKey());
        material.Dispose(); // Disposal is idempotent.
    }

    internal static OwnedProvider CreateProvider(IssuerMaterialFixture fixture)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var material = services.AddIssuerProtection(fixture.Configuration)!;
        try
        {
            return new OwnedProvider(services.BuildServiceProvider(), material);
        }
        catch { material.Dispose(); throw; }
    }

    internal sealed class OwnedProvider(ServiceProvider provider, IssuerCredentials material) : IServiceProvider, IDisposable
    {
        public object? GetService(Type type) => provider.GetService(type);
        public void Dispose()
        {
            provider.Dispose();
            material.Dispose();
        }
    }

    internal static void AssertSafeFailure(IssuerMaterialFixture fixture)
    {
        var error = Assert.Throws<IssuerProtectionException>(() =>
        {
            using var provider = CreateProvider(fixture);
            provider.VerifyIssuerProtection();
        });
        Assert.Equal(IssuerProtectionException.SafeMessage, error.Message);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("synthetic-private-sentinel", error.ToString());
        Assert.DoesNotContain(fixture.Root, error.ToString());
    }
}
