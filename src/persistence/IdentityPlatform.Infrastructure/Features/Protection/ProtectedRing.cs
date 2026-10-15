using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityPlatform.Infrastructure.Features.Protection;

/// <summary>Shares native encrypted ring registration/probes, never application identity, credentials or service-provider ownership.</summary>
internal static class ProtectedRing
{
    private const string Category = "Microsoft.AspNetCore.DataProtection";
    private const string VerificationPurpose = "startup-verification-v1";
    private const string ProbeElement = "probe";
    private const int ProbeBytes = 32;
    private const int ContextBytes = 16;

    /// <summary>Registers a distinct caller-selected application and encrypted repository while suppressing sensitive framework diagnostics.</summary>
    internal static void Register(IServiceCollection services, string application, string directory,
        X509Certificate2 wrapper, X509Certificate2[] decryptors)
    {
        services.AddLogging();
        services.PostConfigure<LoggerFilterOptions>(SuppressPrivateDiagnostics);
        services.AddDataProtection().SetApplicationName(application)
            .PersistKeysToFileSystem(new DirectoryInfo(directory)).ProtectKeysWithCertificate(wrapper)
            .UnprotectKeysWithAnyCertificate(decryptors);
        services.Configure<KeyManagementOptions>(options => options.XmlRepository = new EncryptedKeyRepository(directory));
    }

    /// <summary>Guards provider/category rules on every options rebuild, including runtime configuration reload.</summary>
    private static void SuppressPrivateDiagnostics(LoggerFilterOptions options)
    {
        for (var index = 0; index < options.Rules.Count; index++)
        {
            var rule = options.Rules[index];
            options.Rules[index] = new LoggerFilterRule(rule.ProviderName, rule.CategoryName, rule.LogLevel,
                (provider, name, level) => name?.StartsWith(Category, StringComparison.Ordinal) != true &&
                    (rule.Filter?.Invoke(provider, name, level) ?? true));
        }
        options.Rules.Add(new LoggerFilterRule(null, Category, LogLevel.None, null));
    }

    /// <summary>Proves durable writes, the active wrapper and every retained native decryptor before protecting a startup probe.</summary>
    internal static void Verify(IServiceProvider services, string directory)
    {
        PrivateStorage.VerifyWritableDirectory(directory);
        VerifyWrapper(services);
        foreach (var key in services.GetRequiredService<IKeyManager>().GetAllKeys())
        {
            VerifyKey(key);
        }
        var protector = services.GetRequiredService<IDataProtectionProvider>().CreateProtector(VerificationPurpose);
        var probe = RandomNumberGenerator.GetBytes(ProbeBytes);
        if (!CryptographicOperations.FixedTimeEquals(probe, protector.Unprotect(protector.Protect(probe))))
        {
            throw new ProtectionMaterialException();
        }
        _ = services.GetRequiredService<IKeyManager>().GetAllKeys();
    }

    /// <summary>Tests the configured active wrapper even when a cached native key would otherwise suffice.</summary>
    private static void VerifyWrapper(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        var probe = new XElement(ProbeElement, Convert.ToHexString(RandomNumberGenerator.GetBytes(ProbeBytes)));
        var wrapped = options.XmlEncryptor?.Encrypt(probe) ?? throw new ProtectionMaterialException();
        var unwrapped = new EncryptedXmlDecryptor(services).Decrypt(wrapped.EncryptedElement);
        if (!XNode.DeepEquals(probe, unwrapped))
        {
            throw new ProtectionMaterialException();
        }
    }

    /// <summary>Checks even revoked/retired keys so unreadable persisted material cannot be silently skipped.</summary>
    private static void VerifyKey(IKey key)
    {
        var encryptor = key.CreateEncryptor() ?? throw new ProtectionMaterialException();
        var sample = RandomNumberGenerator.GetBytes(ProbeBytes);
        var context = RandomNumberGenerator.GetBytes(ContextBytes);
        var encrypted = encryptor.Encrypt(new ArraySegment<byte>(sample), new ArraySegment<byte>(context));
        var decrypted = encryptor.Decrypt(new ArraySegment<byte>(encrypted), new ArraySegment<byte>(context));
        if (!CryptographicOperations.FixedTimeEquals(sample, decrypted))
        {
            throw new ProtectionMaterialException();
        }
    }
}
