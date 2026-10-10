using IdentityPlatform.Admin.Api.Features.Storage;
using IdentityPlatform.ServiceDefaults;

namespace IdentityPlatform.Admin.Api.Features;

internal static class Setup
{
    internal static WebApplicationBuilder AddFeatures(this WebApplicationBuilder builder) => builder.AddStorage();

    internal static WebApplication MapFeaturesEndpoints(this WebApplication app) => app.MapPlatformHealthEndpoints();
}
