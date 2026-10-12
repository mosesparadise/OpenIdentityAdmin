using IdentityPlatform.Admin.Bff.Features.ApiContracts;
using IdentityPlatform.Admin.Bff.Features.Storage;
using IdentityPlatform.ServiceDefaults;

namespace IdentityPlatform.Admin.Bff.Features;

internal static class Setup
{
    internal static WebApplicationBuilder AddFeatures(this WebApplicationBuilder builder) => builder.AddStorage().AddApiContracts();

    internal static WebApplication MapFeaturesEndpoints(this WebApplication app) => app.MapPlatformHealthEndpoints();
}
