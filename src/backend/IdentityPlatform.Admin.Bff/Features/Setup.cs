using IdentityPlatform.Admin.Bff.Features.ApiContracts;
using IdentityPlatform.Admin.Bff.Features.Documentation;
using IdentityPlatform.Admin.Bff.Features.Storage;
using IdentityPlatform.ServiceDefaults;

namespace IdentityPlatform.Admin.Bff.Features;

internal static class Setup
{
    /// <summary>Register storage, native contract generation and dormant viewer settings in explicit feature order.</summary>
    internal static WebApplicationBuilder AddFeatures(this WebApplicationBuilder builder) => builder.AddStorage().AddApiContracts().AddDocumentation();

    internal static WebApplication MapFeaturesEndpoints(this WebApplication app) => app.MapPlatformHealthEndpoints();
}
