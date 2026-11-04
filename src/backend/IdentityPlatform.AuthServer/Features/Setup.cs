using IdentityPlatform.AuthServer.Composition;
using IdentityPlatform.AuthServer.Features.Accounts;
using IdentityPlatform.AuthServer.Features.Accounts.Browser;
using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.AuthServer.Features.Issuer;
using IdentityPlatform.AuthServer.Features.LocalDemo;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.AuthServer.Features.Storage;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.ServiceDefaults;

namespace IdentityPlatform.AuthServer.Features;

internal static class Setup
{
    /// <summary>Validates independent feature switches and composes shared account services exactly once.</summary>
    internal static WebApplicationBuilder AddFeatures(this WebApplicationBuilder builder, StartupContext startup)
    {
        builder.PrepareLocalDemo(startup);
        try
        {
            builder.AddStorage();
        }
        catch (ArgumentException)
        {
            throw new DatabaseConfigurationException();
        }
        startup.Credentials = builder.Services.AddIssuer(builder.Configuration);
        startup.AdministratorEnabled = AdministratorConfiguration.IsEnabled(builder.Configuration, startup.Credentials);
        startup.PilotEnabled = PilotConfiguration.IsEnabled(builder.Configuration, builder.Environment, startup.Credentials);
        startup.AdministratorProtocol = AdministratorProtocolConfiguration.Read(builder.Configuration, startup.AdministratorEnabled, startup.Credentials);
        if (startup.LocalDemoEnabled)
        {
            if (startup.PilotEnabled || !startup.AdministratorEnabled || startup.AdministratorProtocol is null)
            {
                throw new LocalDemoConfigurationException();
            }
            startup.AdministratorProtocol = startup.AdministratorProtocol.ForBrowserSessions() ?? throw new LocalDemoConfigurationException();
            builder.Services.AddBrowserSessionProtocol().AddIssuerLogout();
        }
        if (startup.InitializePilot && !startup.PilotEnabled)
        {
            throw new PilotConfigurationException();
        }
        if (startup.PilotEnabled)
        {
            builder.Services.AddPilotServices();
        }
        if (startup.PilotEnabled || startup.AdministratorEnabled)
        {
            builder.Services.AddAccounts(startup.PilotEnabled, startup.AdministratorEnabled,
                startup.LocalDemoEnabled ? new BrowserAccountProfile(LocalDemoProfile.Issuer, LocalDemoProfile.Client) : null, startup.LocalDemoEnabled);
        }
        if (startup.PilotEnabled || startup.AdministratorProtocol is not null)
        {
            builder.Services.AddProtocol(startup.Credentials!, new ProtocolProfiles(startup.PilotEnabled, startup.AdministratorProtocol, startup.LocalDemoEnabled));
        }
        if (startup.AdministratorEnabled)
        {
            builder.Services.AddAdministratorFactors(startup.Credentials!.Issuer);
        }
        return builder;
    }

    /// <summary>Installs account protection independently of pilot protocol activation, retaining health-only defaults.</summary>
    internal static WebApplication MapFeaturesEndpoints(this WebApplication app, StartupContext startup)
    {
        if (startup.PilotEnabled || startup.AdministratorEnabled)
        {
            app.UseRouting();
            app.UseRequestTimeouts();
            app.UseAccountProtection(startup.PilotEnabled);
            if (startup.AdministratorProtocol is not null)
            {
                app.UseProtocolHosting();
            }
            app.UseAuthentication();
            app.UseAuthorization();
            if (startup.AdministratorProtocol is not null)
            {
                app.MapSharedProtocol();
            }
            else if (startup.PilotEnabled)
            {
                app.MapProtocol();
            }
            app.MapAccounts();
        }
        return app.MapPlatformHealthEndpoints();
    }
}
