using IdentityPlatform.AuthServer.Composition;
using IdentityPlatform.AuthServer.Features.Accounts;
using IdentityPlatform.AuthServer.Features.Issuer;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol;
using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.AuthServer.Features.Storage;
using IdentityPlatform.ServiceDefaults;

namespace IdentityPlatform.AuthServer.Features;

internal static class Setup
{
    internal static WebApplicationBuilder AddFeatures(this WebApplicationBuilder builder, StartupContext startup)
    {
        try
        {
            builder.AddStorage();
        }
        catch (ArgumentException)
        {
            throw new DatabaseConfigurationException();
        }
        startup.Credentials = builder.Services.AddIssuer(builder.Configuration);
        startup.PilotEnabled = PilotConfiguration.IsEnabled(builder.Configuration, builder.Environment, startup.Credentials);
        if (startup.InitializePilot && !startup.PilotEnabled)
        {
            throw new PilotConfigurationException();
        }
        if (startup.PilotEnabled)
        {
            builder.Services.AddFirstPartyPilot(startup.Credentials!);
        }
        return builder;
    }

    internal static WebApplication MapFeaturesEndpoints(this WebApplication app, StartupContext startup)
    {
        if (startup.PilotEnabled)
        {
            app.UseRouting();
            app.UseRequestTimeouts();
            app.UsePilotProtection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapProtocol().MapAccounts();
        }
        return app.MapPlatformHealthEndpoints();
    }
}
