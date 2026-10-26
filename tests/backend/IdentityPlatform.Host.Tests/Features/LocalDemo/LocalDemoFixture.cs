using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Core.Features.Configuration;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.Host.Tests.Features.LocalDemo;

/// <summary>Supplies encrypted synthetic role inputs and fresh private staging directories without granting account authority.</summary>
public sealed class LocalDemoFixture : IDisposable
{
    private const string IssuerEnabledSetting = "LocalDemo:Enabled";
    private const string IssuerRootSetting = "LocalDemo:MaterialRoot";
    private const string IssuerEnabled = "true";
    private const string IssuerProviderSetting = "Database:Provider";
    private const string IssuerProvider = "PostgreSql";
    private const string PlatformConnectionSetting = "ConnectionStrings:platform";
    private const string IssuerSetting = "Identity:Issuer";
    private const string AccountsSetting = "Identity:AdministratorAccounts:Enabled";
    private const string ProtocolSetting = "Identity:AdministratorProtocol:Enabled";
    private const string RedirectSetting = "Identity:AdministratorProtocol:RedirectUri";
    private const string IssuerSigningSource = "Identity:Protection:Signing";
    private const string IssuerSigningRole = "IssuerSigning";
    private const string IssuerEncryptionSource = "Identity:Protection:Encryption";
    private const string IssuerEncryptionRole = "IssuerEncryption";
    private const string IssuerWrappingSource = "Identity:Protection:DataProtection:Current";
    private const string IssuerWrappingRole = "IssuerWrapping";
    private const string CredentialsSection = "ClientCredentials";
    private const string PublicKeySetting = "Identity:AdministratorProtocol:PublicKey";
    private const string GuidFormat = "N";
    private const string StateSigningSource = "ClientCredentials:StateSigning";
    private const string StateSigningRole = "ClientStateSigning";
    private const string StateEncryptionSource = "ClientCredentials:StateEncryption";
    private const string StateEncryptionRole = "ClientStateEncryption";
    private const string AssertionSource = "ClientCredentials:AssertionSigning";
    private const string AssertionRole = "ClientAssertion";
    private const string WrappingSource = "Identity:Protection:DataProtection:Current";
    private const string WrappingRole = "ClientWrapping";
    private const string MaterialPrefix = "LocalDemoMaterial:";
    private const string PfxSuffix = ":Pfx";
    private const string PasswordSuffix = ":Password";
    private const string PathSuffix = ":Path";
    private const string EnabledSetting = "LocalDemo:Enabled";
    private const string RootSetting = "LocalDemo:MaterialRoot";
    private const string Enabled = "true";
    private const string ProviderSetting = "Database:Provider";
    private const string Provider = "PostgreSql";
    private const string ConnectionSetting = "ConnectionStrings:adminsessions";
    private const string UnreachableConnection = "Host=localhost;Port=1;Database=synthetic;Username=synthetic;Password=synthetic;Timeout=1";
    private const string TitleSetting = "ApiContracts:Metadata:Title";
    private const string Title = "Synthetic BFF";
    private const string DescriptionSetting = "ApiContracts:Metadata:Description";
    private const string Description = "Synthetic runtime composition";
    private const string PageTitleSetting = "Documentation:Viewer:PageTitle";
    private const string PageTitle = "Synthetic reference";
    private const string BrowserLabelSetting = "Documentation:Viewer:BrowserDocumentLabel";
    private const string BrowserLabel = "Browser v1";
    private const string AdminLabelSetting = "Documentation:Viewer:AdminDocumentLabel";
    private const string AdminLabel = "Admin v1";
    private readonly IssuerMaterialFixture issuer = new();
    private readonly NativeClientCredentialFixture client = new();

    /// <summary>Builds the normal issuer's exact local settings and public-only client registration binding.</summary>
    internal Dictionary<string, string?> IssuerSettings(string root)
    {
        var settings = new Dictionary<string, string?>
        {
            [IssuerEnabledSetting] = IssuerEnabled,
            [IssuerRootSetting] = root,
            [IssuerProviderSetting] = IssuerProvider,
            [PlatformConnectionSetting] = UnreachableConnection,
            [IssuerSetting] = LocalDemoProfile.Issuer,
            [AccountsSetting] = IssuerEnabled,
            [ProtocolSetting] = IssuerEnabled,
            [RedirectSetting] = LocalDemoProfile.Callback
        };
        Copy(issuer.Settings, IssuerSigningSource, IssuerSigningRole, settings);
        Copy(issuer.Settings, IssuerEncryptionSource, IssuerEncryptionRole, settings);
        Copy(issuer.Settings, IssuerWrappingSource, IssuerWrappingRole, settings);
        using var credentials = NativeClientCredentials.Load(new ConfigurationBuilder().AddInMemoryCollection(client.Settings()).Build().GetSection(CredentialsSection), DateTimeOffset.UtcNow);
        var key = NativeClientProfile.Create(LocalDemoProfile.Issuer, LocalDemoProfile.Client, LocalDemoProfile.Callback,
            LocalDemoProfile.AssertionKeyId, credentials).Registration.Key.CreateNativeKey();
        settings[PublicKeySetting] = JsonSerializer.Serialize(new
        {
            kty = key.Kty,
            kid = key.Kid,
            alg = key.Alg,
            use = key.Use,
            n = key.N,
            e = key.E
        });
        return settings;
    }

    /// <summary>Creates an isolated existing mount equivalent beneath the fixture's private parent.</summary>
    internal string CreateRoot()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException();
        }
        var root = Path.Combine(issuer.Root, Guid.NewGuid().ToString(GuidFormat));
        Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return root;
    }

    /// <summary>Maps existing distinct synthetic client roles plus a separate wrapper into demo transport input.</summary>
    internal Dictionary<string, string?> BffSettings(string root)
    {
        var settings = BaseSettings(root);
        var source = client.Settings();
        Copy(source, StateSigningSource, StateSigningRole, settings);
        Copy(source, StateEncryptionSource, StateEncryptionRole, settings);
        Copy(source, AssertionSource, AssertionRole, settings);
        Copy(issuer.Settings, WrappingSource, WrappingRole, settings);
        return settings;
    }

    /// <summary>Produces bounded encrypted certificate inputs from test-only private fixture files.</summary>
    private static void Copy(Dictionary<string, string?> source, string prefix, string role, Dictionary<string, string?> target)
    {
        target[MaterialPrefix + role + PfxSuffix] = Convert.ToBase64String(File.ReadAllBytes(source[prefix + PathSuffix]!));
        target[MaterialPrefix + role + PasswordSuffix] = source[prefix + PasswordSuffix];
    }

    /// <summary>Uses actual metadata configuration and an unreachable database so accidental route I/O cannot pass silently.</summary>
    internal static Dictionary<string, string?> BaseSettings(string root) => new()
    {
        [EnabledSetting] = Enabled,
        [RootSetting] = root,
        [ProviderSetting] = Provider,
        [ConnectionSetting] = UnreachableConnection,
        [TitleSetting] = Title,
        [DescriptionSetting] = Description,
        [PageTitleSetting] = PageTitle,
        [BrowserLabelSetting] = BrowserLabel,
        [AdminLabelSetting] = AdminLabel
    };

    /// <summary>Removes only fixture-owned directories after all tested consumers and startup owners have stopped.</summary>
    public void Dispose()
    {
        client.Dispose();
        issuer.Dispose();
    }
}
