using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using IdentityPlatform.Infrastructure.Features.Protection;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;

/// <summary>Owns durable, role-separated client certificates loaded from protected files; dispose only after all native client consumers.</summary>
internal sealed class NativeClientCredentials : IDisposable
{
    internal const string StateSigningSection = "StateSigning";
    internal const string StateEncryptionSection = "StateEncryption";
    internal const string AssertionSection = "AssertionSigning";
    internal const string PathSetting = "Path";
    internal const string PasswordSetting = "Password";
    private const string PreviousSection = "Previous";
    private const int MaximumPrevious = 4;
    private readonly List<X509Certificate2> owned = [];
    private readonly HashSet<string> fingerprints = new(StringComparer.Ordinal);
    internal ClientStateKeySet StateSigning { get; private set; } = null!;
    internal ClientStateKeySet StateEncryption { get; private set; } = null!;
    internal X509Certificate2 Assertion { get; private set; } = null!;

    /// <summary>Constructs an empty owner so partial loads always have one deterministic disposal path.</summary>
    private NativeClientCredentials()
    {
    }

    /// <summary>Requires all three explicit roles; unsupported fields and transient-key fallback cannot silently alter the profile.</summary>
    internal static NativeClientCredentials Load(IConfigurationSection section, DateTimeOffset now)
    {
        var material = new NativeClientCredentials();
        try
        {
            RequireFields(section, StateSigningSection, StateEncryptionSection, AssertionSection);
            material.StateSigning = material.LoadState(section.GetSection(StateSigningSection), X509KeyUsageFlags.DigitalSignature, now);
            material.StateEncryption = material.LoadState(section.GetSection(StateEncryptionSection), X509KeyUsageFlags.KeyEncipherment, now);
            var assertion = section.GetSection(AssertionSection);
            RequireFields(assertion, PathSetting, PasswordSetting);
            material.Assertion = material.LoadCertificate(assertion, X509KeyUsageFlags.DigitalSignature, now, retained: false);
            return material;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException or
            UnauthorizedAccessException or CryptographicException or FormatException or NotSupportedException)
        {
            material.Dispose();
            throw new ClientCredentialFailure();
        }
    }

    /// <summary>Loads one active credential and no more than four explicitly indexed retained state credentials.</summary>
    private ClientStateKeySet LoadState(IConfigurationSection section, X509KeyUsageFlags usage, DateTimeOffset now)
    {
        RequireFields(section, PathSetting, PasswordSetting, PreviousSection);
        var active = LoadCertificate(section, usage, now, retained: false);
        var previous = section.GetSection(PreviousSection);
        var entries = previous.GetChildren().ToArray();
        if (previous.Value is not null || entries.Length > MaximumPrevious)
        {
            throw new ClientCredentialFailure();
        }
        var retained = new List<X509Certificate2>();
        for (var index = 0; index < entries.Length; index++)
        {
            if (entries[index].Key != index.ToString(CultureInfo.InvariantCulture))
            {
                throw new ClientCredentialFailure();
            }
            RequireFields(entries[index], PathSetting, PasswordSetting);
            retained.Add(LoadCertificate(entries[index], usage, now, retained: true));
        }
        return new(active, retained.ToArray());
    }

    /// <summary>Reuses bounded encrypted PKCS#12/private-directory loading and owns each certificate before validating its role.</summary>
    private X509Certificate2 LoadCertificate(IConfigurationSection section, X509KeyUsageFlags usage, DateTimeOffset now, bool retained)
    {
        var certificate = ProtectedCertificate.Load(section[PathSetting], section[PasswordSetting]);
        owned.Add(certificate);
        ClientCertificatePolicy.Require(certificate, usage, now, retained, fingerprints);
        return certificate;
    }

    /// <summary>Rejects unknown settings and scalar/object ambiguity without exposing any configured values.</summary>
    private static void RequireFields(IConfigurationSection section, params string[] allowed)
    {
        if (section.Value is not null || section.GetChildren().Any(child => !allowed.Contains(child.Key, StringComparer.OrdinalIgnoreCase) ||
            (child.Key.Equals(PathSetting, StringComparison.OrdinalIgnoreCase) || child.Key.Equals(PasswordSetting, StringComparison.OrdinalIgnoreCase)) && child.GetChildren().Any()))
        {
            throw new ClientCredentialFailure();
        }
    }

    /// <summary>Disposes all successfully imported credentials, including material loaded before a later validation failure.</summary>
    public void Dispose()
    {
        foreach (var certificate in owned)
        {
            certificate.Dispose();
        }
        owned.Clear();
    }
}
