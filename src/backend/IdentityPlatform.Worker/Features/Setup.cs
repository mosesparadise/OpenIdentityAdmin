using IdentityPlatform.ServiceDefaults;
using IdentityPlatform.Worker.Features.ApplicationGrants;
using IdentityPlatform.Worker.Features.Storage;
using IdentityPlatform.Worker.Features.Catalogue;

namespace IdentityPlatform.Worker.Features;

internal static class Setup
{
    /// <summary>Composes provider storage before the bounded durable grant processor without issuer-only key services.</summary>
    internal static WebApplicationBuilder AddFeatures(this WebApplicationBuilder builder) => builder.AddStorage().AddGrantProcessing().AddCatalogueProcessing();

    /// <summary>Exposes the shared liveness and provider readiness endpoints for the ordinary Worker host.</summary>
    internal static WebApplication MapFeaturesEndpoints(this WebApplication app) => app.MapPlatformHealthEndpoints();
}
