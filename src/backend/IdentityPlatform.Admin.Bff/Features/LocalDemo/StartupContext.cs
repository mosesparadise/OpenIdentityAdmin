using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Protection;

namespace IdentityPlatform.Admin.Bff.Features.LocalDemo;

/// <summary>Owns disposable BFF material outside the generic host so every native consumer stops before key disposal.</summary>
internal sealed class StartupContext : IDisposable
{
    private const string RootSetting = "LocalDemo:MaterialRoot";
    private const string RingSetting = "Bff:Sessions:Protection:KeyRingPath";
    private const string ApplicationSetting = "Bff:Sessions:Protection:ApplicationId";
    private const string ApplicationId = "local-demo";
    private const string ClientCredentials = "Bff:Client:Credentials";
    private static readonly Dictionary<string, string> Roles = new(StringComparer.Ordinal)
    {
        ["ClientStateSigning"] = ClientCredentials + ":StateSigning",
        ["ClientStateEncryption"] = ClientCredentials + ":StateEncryption",
        ["ClientAssertion"] = ClientCredentials + ":AssertionSigning",
        ["ClientWrapping"] = "Bff:Sessions:Protection:Current"
    };
    private LocalDemoMaterial? material;
    private NativeClientCredentials? credentials;
    private SessionProtection? protection;
    internal bool Enabled
    {
        get; private set;
    }

    /// <summary>Requires explicit Development activation, then composes existing fixed native identities and protected sessions.</summary>
    internal void Configure(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        Enabled = LocalDemoConfiguration.IsEnabled(configuration, environment);
        if (!Enabled)
        {
            return;
        }
        material = LocalDemoMaterial.Create(configuration, configuration[RootSetting] ?? LocalDemoProfile.MaterialRoot,
            Roles, RingSetting);
        material.Settings.Add(ApplicationSetting, ApplicationId);
        var settings = new ConfigurationBuilder().AddConfiguration(configuration).AddInMemoryCollection(material.Settings).Build();
        credentials = NativeClientCredentials.Load(settings.GetSection(ClientCredentials), DateTimeOffset.UtcNow);
        protection = SessionProtection.Create(settings);
        var profile = NativeClientProfile.Create(LocalDemoProfile.Issuer, LocalDemoProfile.Client,
            LocalDemoProfile.Callback, LocalDemoProfile.AssertionKeyId, credentials).ForBrowserSessions();
        services.AddAdministratorClient(profile, credentials);
        services.AddBrowserLogin(new BrowserSessionDestinations(LocalDemoProfile.Issuer, LocalDemoProfile.Client), protection);
    }

    /// <summary>Disposes protection consumers and native keys before deleting this owner's files, including partial startup.</summary>
    public void Dispose()
    {
        try
        {
            protection?.Dispose();
        }
        finally
        {
            try
            {
                credentials?.Dispose();
            }
            finally { material?.Dispose(); }
        }
    }
}
