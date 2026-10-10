using IdentityPlatform.AuthServer.Features;
using IdentityPlatform.AuthServer.Features.Issuer;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.ServiceDefaults;

namespace IdentityPlatform.AuthServer.Composition;

internal static class WebApplicationExtensions
{
    internal static WebApplicationBuilder AddConfigurations(this WebApplicationBuilder builder) => builder.AddPlatformServiceDefaults();

    internal static WebApplicationBuilder ConfigureServices(this WebApplicationBuilder builder, StartupContext startup) => builder.AddFeatures(startup);

    internal static WebApplication ConfigureApplication(this WebApplication app, StartupContext startup)
    {
        app.VerifyIssuer(startup.Credentials);
        // A one-shot bootstrap never composes HTTP, including Razor/embedded assets.
        return startup.InitializePilot ? app : app.MapFeaturesEndpoints(startup);
    }

    internal static async Task<int> RunApplicationAsync(this WebApplication app, StartupContext startup)
    {
        if (startup.InitializePilot)
        {
            return await app.InitializePilotAsync();
        }
        await app.RunAsync();
        return 0;
    }
}
