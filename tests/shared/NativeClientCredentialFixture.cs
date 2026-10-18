using System.Security.Cryptography.X509Certificates;

namespace IdentityPlatform.Testing;

/// <summary>Shares only encrypted synthetic source files; each tested load owns independent imported certificate objects.</summary>
public sealed class NativeClientCredentialFixture : IDisposable
{
    private const string SourcePrefix = "Identity:Protection:";
    private const string TargetPrefix = "ClientCredentials:";
    private const string Assertion = "ClientAssertion";
    private const string Signing = "Signing";
    private const string Encryption = "Encryption";
    private const string StateSigning = "StateSigning";
    private const string StateEncryption = "StateEncryption";
    private const string AssertionSigning = "AssertionSigning";
    private const string PathSuffix = ":Path";
    private const string PasswordSuffix = ":Password";
    private readonly IssuerMaterialFixture material = new();

    /// <summary>Generates one additional test-only signing key so assertion and state roles cannot share public key material.</summary>
    public NativeClientCredentialFixture() => material.AddCertificate(Assertion, X509KeyUsageFlags.DigitalSignature);

    /// <summary>Returns a fresh settings dictionary for each independent corruption/configuration control.</summary>
    internal Dictionary<string, string?> Settings()
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (source, target) in new[] { (Signing, StateSigning), (Encryption, StateEncryption), (Assertion, AssertionSigning) })
        {
            result[TargetPrefix + target + PathSuffix] = material.Settings[SourcePrefix + source + PathSuffix];
            result[TargetPrefix + target + PasswordSuffix] = material.Settings[SourcePrefix + source + PasswordSuffix];
        }
        return result;
    }

    /// <summary>Creates replacement state keys with shorter validity, retaining the originals so native sorting alone would prefer old material.</summary>
    internal Dictionary<string, string?> RotatedSettings()
    {
        const string RotatedSigning = "RotatedSigning";
        const string RotatedEncryption = "RotatedEncryption";
        const string Previous = ":Previous:0";
        const int ReplacementDays = 10;
        var result = Settings();
        material.AddCertificate(RotatedSigning, X509KeyUsageFlags.DigitalSignature, notAfter: DateTimeOffset.UtcNow.AddDays(ReplacementDays));
        material.AddCertificate(RotatedEncryption, X509KeyUsageFlags.KeyEncipherment, notAfter: DateTimeOffset.UtcNow.AddDays(ReplacementDays));
        foreach (var (source, target) in new[] { (RotatedSigning, StateSigning), (RotatedEncryption, StateEncryption) })
        {
            result[TargetPrefix + target + Previous + PathSuffix] = result[TargetPrefix + target + PathSuffix];
            result[TargetPrefix + target + Previous + PasswordSuffix] = result[TargetPrefix + target + PasswordSuffix];
            result[TargetPrefix + target + PathSuffix] = material.Settings[SourcePrefix + source + PathSuffix];
            result[TargetPrefix + target + PasswordSuffix] = material.Settings[SourcePrefix + source + PasswordSuffix];
        }
        return result;
    }

    /// <summary>Removes only the fixture's private synthetic directory after all certificate consumers have been disposed.</summary>
    public void Dispose() => material.Dispose();
}
