using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using IdentityPlatform.Infrastructure.Features.Protection;
using Microsoft.AspNetCore.DataProtection;

namespace IdentityPlatform.Admin.Bff.Features.Sessions;

/// <summary>Owns an isolated BFF protection provider and its wrappers; dispose after every store consumer has stopped.</summary>
internal sealed class SessionProtection : IDisposable
{
    private const string SectionName = "Bff:Sessions:Protection";
    private const string ApplicationSetting = "ApplicationId";
    private const string RingSetting = "KeyRingPath";
    private const string CurrentSection = "Current";
    private const string PreviousSection = "Previous";
    private const string ApplicationPrefix = "IdentityPlatform.Admin.Bff|";
    private static readonly string[] ScalarSettings = [ApplicationSetting, RingSetting, "Path", "Password"];
    private const int ApplicationLimit = 100;
    private const int RetainedLimit = 4;
    private readonly List<X509Certificate2> certificates = [];
    private ServiceProvider? services;
    internal IDataProtectionProvider Provider => services?.GetRequiredService<IDataProtectionProvider>() ?? throw new SessionProtectionException();

    /// <summary>Validates explicit deployment identity, private durable storage and wrapping material before returning a usable provider.</summary>
    internal static SessionProtection Create(IConfiguration configuration)
    {
        var owner = new SessionProtection();
        try
        {
            var section = configuration.GetSection(SectionName);
            RequireFields(section, ApplicationSetting, RingSetting, CurrentSection, PreviousSection);
            var application = section[ApplicationSetting];
            if (string.IsNullOrEmpty(application) || application.Length > ApplicationLimit ||
                application.Any(value => !char.IsAsciiLetterOrDigit(value) && value is not '.' and not '_' and not '-'))
            {
                throw new SessionProtectionException();
            }
            var directory = PrivateStorage.RequireDirectory(section[RingSetting]);
            var wrapper = owner.Load(section.GetSection(CurrentSection), active: true);
            owner.LoadPrevious(section.GetSection(PreviousSection));
            var registrations = new ServiceCollection();
            ProtectedRing.Register(registrations, ApplicationPrefix + application, directory, wrapper, [.. owner.certificates]);
            owner.services = registrations.BuildServiceProvider(validateScopes: true);
            ProtectedRing.Verify(owner.services, directory);
            return owner;
        }
        catch (Exception exception) when (IsConfigurationFailure(exception))
        {
            owner.Dispose();
            throw new SessionProtectionException();
        }
    }

    /// <summary>Requires contiguous bounded retained wrapper entries and rejects duplicate private key identities.</summary>
    private void LoadPrevious(IConfigurationSection section)
    {
        var entries = section.GetChildren().ToArray();
        if (section.Value is not null || entries.Length > RetainedLimit)
        {
            throw new SessionProtectionException();
        }
        for (var index = 0; index < entries.Length; index++)
        {
            if (entries[index].Key != index.ToString(System.Globalization.CultureInfo.InvariantCulture))
            {
                throw new SessionProtectionException();
            }
            _ = Load(entries[index], active: false);
        }
        var fingerprints = certificates.Select(certificate => Convert.ToHexString(SHA256.HashData(certificate.GetPublicKey())));
        if (fingerprints.Distinct(StringComparer.Ordinal).Count() != certificates.Count)
        {
            throw new SessionProtectionException();
        }
    }

    /// <summary>Loads an owned wrapper before validation so every partial failure releases its ephemeral private key.</summary>
    private X509Certificate2 Load(IConfigurationSection section, bool active)
    {
        const string PathSetting = "Path";
        const string PasswordSetting = "Password";
        const int MinimumRsaBits = 3072;
        const int ProbeBytes = 32;
        RequireFields(section, PathSetting, PasswordSetting);
        var certificate = ProtectedCertificate.Load(section[PathSetting], section[PasswordSetting]);
        certificates.Add(certificate);
        using var key = certificate.GetRSAPrivateKey();
        using var publicKey = certificate.GetRSAPublicKey();
        var usage = certificate.Extensions.OfType<X509KeyUsageExtension>().SingleOrDefault();
        var now = DateTimeOffset.UtcNow;
        if (key is null || publicKey is null || key.KeySize < MinimumRsaBits || usage is null ||
            (usage.KeyUsages & X509KeyUsageFlags.KeyEncipherment) == 0 ||
            active && (now < certificate.NotBefore.ToUniversalTime() || now >= certificate.NotAfter.ToUniversalTime()))
        {
            throw new SessionProtectionException();
        }
        var probe = RandomNumberGenerator.GetBytes(ProbeBytes);
        var decrypted = key.Decrypt(publicKey.Encrypt(probe, RSAEncryptionPadding.OaepSHA256), RSAEncryptionPadding.OaepSHA256);
        if (!CryptographicOperations.FixedTimeEquals(probe, decrypted))
        {
            throw new SessionProtectionException();
        }
        return certificate;
    }

    /// <summary>Rejects unknown configuration fields instead of silently ignoring misspelled protection policy.</summary>
    private static void RequireFields(IConfigurationSection section, params string[] allowed)
    {
        if (section.Value is not null || section.GetChildren().Any(child => !allowed.Contains(child.Key, StringComparer.OrdinalIgnoreCase) ||
            ScalarSettings.Contains(child.Key, StringComparer.OrdinalIgnoreCase) && child.GetChildren().Any()))
        {
            throw new SessionProtectionException();
        }
    }

    /// <summary>Disposes all native consumers before wrapper private keys, including partially initialized owners.</summary>
    public void Dispose()
    {
        try
        {
            services?.Dispose();
        }
        finally
        {
            services = null;
            foreach (var certificate in certificates)
            {
                certificate.Dispose();
            }
            certificates.Clear();
        }
    }

    /// <summary>Collapses expected configuration failures without disclosing native exception messages.</summary>
    private static bool IsConfigurationFailure(Exception exception) => exception is
        SessionProtectionException or ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or
        CryptographicException or XmlException or FormatException or NotSupportedException;
}

/// <summary>Reports a fixed startup failure without secrets, filenames or underlying exceptions.</summary>
internal sealed class SessionProtectionException() : Exception(SafeMessage)
{
    internal const string SafeMessage = "Invalid BFF session protection configuration or key ring.";
}
