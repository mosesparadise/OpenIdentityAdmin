using System.Security.Cryptography;
using System.Xml;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.Infrastructure.Features.Protection;

namespace IdentityPlatform.AuthServer.Features.Issuer.DataProtection;

/// <summary>Preserves issuer-specific credential ownership while sharing validated native ring mechanics.</summary>
/// <example><code>using var credentials = services.AddIssuerProtection(configuration); // Dispose after the host.</code></example>
public static class IssuerProtectionRegistration
{
    private const string ApplicationPrefix = "IdentityPlatform.AuthServer|";
    private const string IdentitySection = "Identity";
    private const string IssuerSetting = "Issuer";
    private const string ProtectionSection = "Protection";

    /// <summary>Registers the issuer ring; the caller owns credentials and must dispose them after its host.</summary>
    /// <example><code>using var credentials = builder.Services.AddIssuerProtection(builder.Configuration);</code></example>
    public static IssuerCredentials? AddIssuerProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var identity = configuration.GetSection(IdentitySection);
        if (!identity.GetSection(IssuerSetting).Exists() && !identity.GetSection(ProtectionSection).Exists())
        {
            return null;
        }
        var credentials = IssuerCredentials.Load(configuration, DateTimeOffset.UtcNow);
        try
        {
            services.AddSingleton(credentials);
            ProtectedRing.Register(services, ApplicationPrefix + credentials.Issuer.AbsoluteUri,
                credentials.KeyRingPath, credentials.Wrapping, credentials.WrappingDecryptors.ToArray());
            return credentials;
        }
        catch { credentials.Dispose(); throw; }
    }

    /// <summary>Fails safely unless active and retained encrypted native keys can perform real operations.</summary>
    /// <example><code>app.Services.VerifyIssuerProtection(); // Before accepting requests.</code></example>
    public static void VerifyIssuerProtection(this IServiceProvider services)
    {
        try
        {
            ProtectedRing.Verify(services, services.GetRequiredService<IssuerCredentials>().KeyRingPath);
        }
        catch (Exception exception) when (IsConfigurationFailure(exception)) { throw new IssuerProtectionException(); }
    }

    /// <summary>Classifies startup failures without retaining underlying messages or configuration values.</summary>
    internal static bool IsConfigurationFailure(Exception exception) => exception is
        IssuerProtectionException or ArgumentException or InvalidOperationException or IOException or
        UnauthorizedAccessException or CryptographicException or XmlException or FormatException or NotSupportedException;
}
