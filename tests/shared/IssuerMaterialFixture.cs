using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.Testing;

// Only tests generate synthetic issuer certificates. Runtime never calls this fixture.
internal sealed class IssuerMaterialFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "identity-keys-" + Guid.NewGuid().ToString("N"));
    public string Ring => Path.Combine(Root, "ring");
    public Dictionary<string, string?> Settings { get; } = new(StringComparer.OrdinalIgnoreCase);
    public IConfiguration Configuration => new ConfigurationBuilder().AddInMemoryCollection(Settings).Build();

    public IssuerMaterialFixture()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Issuer filesystem tests require Linux.");
        Directory.CreateDirectory(Root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Directory.CreateDirectory(Ring, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Settings["Identity:Issuer"] = "https://issuer.example.test/";
        Settings["Identity:Protection:DataProtection:KeyRingPath"] = Ring;
        AddCertificate("Signing", X509KeyUsageFlags.DigitalSignature);
        AddCertificate("Encryption", X509KeyUsageFlags.KeyEncipherment);
        AddCertificate("DataProtection:Current", X509KeyUsageFlags.KeyEncipherment);
    }

    public void AddCertificate(string section, X509KeyUsageFlags? usage, int keySize = 3072,
        DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null, RSA? sharedKey = null,
        bool publicOnly = false, bool elliptic = false)
    {
        using var ownedKey = sharedKey is null ? RSA.Create(keySize) : null;
        using var ec = elliptic ? ECDsa.Create(ECCurve.NamedCurves.nistP256) : null;
        var request = elliptic
            ? new CertificateRequest("CN=synthetic-issuer", ec!, HashAlgorithmName.SHA256)
            : new CertificateRequest("CN=synthetic-issuer", sharedKey ?? ownedKey!, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        if (usage is not null) request.CertificateExtensions.Add(new X509KeyUsageExtension(usage.Value, critical: true));
        using var certificate = request.CreateSelfSigned(notBefore ?? DateTimeOffset.UtcNow.AddMinutes(-5),
            notAfter ?? DateTimeOffset.UtcNow.AddDays(30));
        using var exported = publicOnly ? X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert)) : null;
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var path = Path.Combine(Root, Guid.NewGuid().ToString("N") + ".pfx");
        WritePrivate(path, (exported ?? certificate).Export(X509ContentType.Pfx, password));
        Settings[$"Identity:Protection:{section}:Path"] = path;
        Settings[$"Identity:Protection:{section}:Password"] = password;
    }

    public void RetainCurrentWrapper()
    {
        Settings["Identity:Protection:DataProtection:Previous:0:Path"] = Settings["Identity:Protection:DataProtection:Current:Path"];
        Settings["Identity:Protection:DataProtection:Previous:0:Password"] = Settings["Identity:Protection:DataProtection:Current:Password"];
        AddCertificate("DataProtection:Current", X509KeyUsageFlags.KeyEncipherment);
    }

    public static void WritePrivate(string path, byte[] bytes)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Issuer filesystem tests require Linux.");
        using var file = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.Write,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite
        });
        file.Write(bytes);
        CryptographicOperations.ZeroMemory(bytes);
    }

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
