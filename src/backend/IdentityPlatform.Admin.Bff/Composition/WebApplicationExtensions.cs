using IdentityPlatform.Admin.Bff.Features;
using IdentityPlatform.ServiceDefaults;

namespace IdentityPlatform.Admin.Bff.Composition;

internal static class WebApplicationExtensions
{
    internal static WebApplicationBuilder AddConfigurations(this WebApplicationBuilder builder) => builder.AddPlatformServiceDefaults();

    internal static WebApplicationBuilder ConfigureServices(this WebApplicationBuilder builder) => builder.AddFeatures();

    internal static WebApplication ConfigureApplication(this WebApplication app) => app.MapFeaturesEndpoints();
}
