using IdentityPlatform.AuthServer.Composition;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Protection;

namespace IdentityPlatform.AuthServer.Features.LocalDemo;

/// <summary>Prepares the opt-in disposable issuer before existing credential and account validation.</summary>
internal static class Setup
{
    private const string IssuerSetting = "Identity:Issuer";
    private const string RedirectSetting = "Identity:AdministratorProtocol:RedirectUri";
    private const string RootSetting = "LocalDemo:MaterialRoot";
    private const string RingSetting = "Identity:Protection:DataProtection:KeyRingPath";
    private static readonly Dictionary<string, string> Roles = new(StringComparer.Ordinal)
    {
        ["IssuerSigning"] = "Identity:Protection:Signing",
        ["IssuerEncryption"] = "Identity:Protection:Encryption",
        ["IssuerWrapping"] = "Identity:Protection:DataProtection:Current"
    };

    /// <summary>Rejects conflicting profile identity and stages only issuer-owned private material on Linux.</summary>
    internal static void PrepareLocalDemo(this WebApplicationBuilder builder, StartupContext startup)
    {
        startup.LocalDemoEnabled = LocalDemoConfiguration.IsEnabled(builder.Configuration, builder.Environment);
        if (!startup.LocalDemoEnabled)
        {
            return;
        }
        if (startup.InitializePilot || builder.Configuration[IssuerSetting] != LocalDemoProfile.Issuer ||
            builder.Configuration[RedirectSetting] != LocalDemoProfile.Callback)
        {
            throw new LocalDemoConfigurationException();
        }
        startup.DemoMaterial = LocalDemoMaterial.Create(builder.Configuration,
            builder.Configuration[RootSetting] ?? LocalDemoProfile.MaterialRoot, Roles, RingSetting);
        builder.Configuration.AddInMemoryCollection(startup.DemoMaterial.Settings);
    }
}
