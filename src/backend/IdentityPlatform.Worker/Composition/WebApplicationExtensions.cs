using IdentityPlatform.ServiceDefaults;
using IdentityPlatform.Worker.Features;

namespace IdentityPlatform.Worker.Composition;

internal static class WebApplicationExtensions
{
    internal static WebApplicationBuilder AddConfigurations(this WebApplicationBuilder builder) => builder.AddPlatformServiceDefaults();

    internal static WebApplicationBuilder ConfigureServices(this WebApplicationBuilder builder) => builder.AddFeatures();

    internal static WebApplication ConfigureApplication(this WebApplication app) => app.MapFeaturesEndpoints();
}
