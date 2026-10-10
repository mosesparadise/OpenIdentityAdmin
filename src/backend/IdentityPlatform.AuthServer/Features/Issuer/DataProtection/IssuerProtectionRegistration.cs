using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Options;

namespace IdentityPlatform.AuthServer.Features.Issuer.DataProtection;

public static class IssuerProtectionRegistration
{
    /// <summary>The caller owns the returned credentials and must dispose them AFTER its host/provider.</summary>
    public static IssuerCredentials? AddIssuerProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var identity = configuration.GetSection("Identity");
        if (!identity.GetSection("Issuer").Exists() && !identity.GetSection("Protection").Exists())
        {
            return null;
        }

        var credentials = IssuerCredentials.Load(configuration, DateTimeOffset.UtcNow);
        try
        {
            // External singleton ownership intentionally remains at the composition root.
            services.AddSingleton(credentials);
            services.AddLogging();
            services.PostConfigure<LoggerFilterOptions>(options =>
            {
                const string category = "Microsoft.AspNetCore.DataProtection";
                // Category/provider-specific configuration normally overrides a parent AddFilter.
                // Guard every existing rule too, including rules rebuilt by configuration reload.
                for (var index = 0; index < options.Rules.Count; index++)
                {
                    var rule = options.Rules[index];
                    options.Rules[index] = new LoggerFilterRule(rule.ProviderName, rule.CategoryName, rule.LogLevel,
                        (provider, name, level) => name?.StartsWith(category, StringComparison.Ordinal) != true &&
                            (rule.Filter?.Invoke(provider, name, level) ?? true));
                }
                options.Rules.Add(new LoggerFilterRule(null, category, LogLevel.None, null));
            });
            services.AddDataProtection().SetApplicationName("IdentityPlatform.AuthServer|" + credentials.Issuer.AbsoluteUri)
                .PersistKeysToFileSystem(new DirectoryInfo(credentials.KeyRingPath))
                .ProtectKeysWithCertificate(credentials.Wrapping)
                .UnprotectKeysWithAnyCertificate(credentials.WrappingDecryptors.ToArray());
            services.Configure<KeyManagementOptions>(options => options.XmlRepository = new EncryptedKeyRepository(credentials.KeyRingPath));
            return credentials;
        }
        catch
        {
            credentials.Dispose();
            throw;
        }
    }

    public static void VerifyIssuerProtection(this IServiceProvider services)
    {
        try
        {
            var credentials = services.GetRequiredService<IssuerCredentials>();
            PrivateStorage.VerifyWritableDirectory(credentials.KeyRingPath);
            var options = services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
            // Verify the active wrapper even when a previously cached DP key would otherwise suffice.
            var xmlProbe = new XElement("probe", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
            var wrapped = options.XmlEncryptor!.Encrypt(xmlProbe);
            var unwrapped = new EncryptedXmlDecryptor(services).Decrypt(wrapped.EncryptedElement);
            if (!XNode.DeepEquals(xmlProbe, unwrapped))
            {
                throw new IssuerProtectionException();
            }

            var keys = services.GetRequiredService<IKeyManager>().GetAllKeys();
            foreach (var key in keys)
            {
                var encryptor = key.CreateEncryptor() ?? throw new IssuerProtectionException();
                var sample = RandomNumberGenerator.GetBytes(32);
                var context = RandomNumberGenerator.GetBytes(16);
                var encrypted = encryptor.Encrypt(new ArraySegment<byte>(sample), new ArraySegment<byte>(context));
                var decrypted = encryptor.Decrypt(new ArraySegment<byte>(encrypted), new ArraySegment<byte>(context));
                if (!CryptographicOperations.FixedTimeEquals(sample, decrypted))
                {
                    throw new IssuerProtectionException();
                }
            }
            var protector = services.GetRequiredService<IDataProtectionProvider>().CreateProtector("startup-verification-v1");
            var probe = RandomNumberGenerator.GetBytes(32);
            var protectedProbe = protector.Protect(probe);
            if (!CryptographicOperations.FixedTimeEquals(probe, protector.Unprotect(protectedProbe)))
            {
                throw new IssuerProtectionException();
            }
            // Read again to validate the first native key just persisted by a previously empty ring.
            _ = services.GetRequiredService<IKeyManager>().GetAllKeys();
        }
        catch (Exception exception) when (IsConfigurationFailure(exception))
        {
            throw new IssuerProtectionException();
        }
    }

    internal static bool IsConfigurationFailure(Exception exception) => exception is
        IssuerProtectionException or ArgumentException or InvalidOperationException or IOException or
        UnauthorizedAccessException or CryptographicException or XmlException or FormatException or NotSupportedException;
}
