using IdentityPlatform.ServiceDefaults;
using IdentityPlatform.Worker.Features.Storage;

namespace IdentityPlatform.Worker.Features;

internal static class Setup
{
    internal static WebApplicationBuilder AddFeatures(this WebApplicationBuilder builder) => builder.AddStorage();

    internal static WebApplication MapFeaturesEndpoints(this WebApplication app) => app.MapPlatformHealthEndpoints();
}
