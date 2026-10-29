using IdentityPlatform.Admin.Bff.Features.AdminUi;
using IdentityPlatform.Admin.Bff.Features.ApiContracts;
using IdentityPlatform.Admin.Bff.Features.Applications;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Documentation;
using IdentityPlatform.Admin.Bff.Features.LocalDemo;
using IdentityPlatform.Admin.Bff.Features.Storage;
using IdentityPlatform.ServiceDefaults;

namespace IdentityPlatform.Admin.Bff.Features;

internal static class Setup
{
    /// <summary>Register storage, native contract generation and dormant viewer settings in explicit feature order.</summary>
    internal static WebApplicationBuilder AddFeatures(this WebApplicationBuilder builder) => builder.AddStorage().AddApiContracts().AddDocumentation();

    /// <summary>Registers the same validated features for explicit generic web hosting.</summary>
    internal static IServiceCollection AddFeatures(this IServiceCollection services, IConfiguration configuration) =>
        services.AddStorage(configuration).AddApiContracts(configuration).AddDocumentation(configuration);

    /// <summary>Installs owned product routes before explicit operational endpoints, with no global native authentication dispatch.</summary>
    internal static IApplicationBuilder MapFeaturesEndpoints(this IApplicationBuilder app, StartupContext startup)
    {
        if (startup.Enabled)
        {
            app.UseBrowserLogin();
            app.UseAdministratorUi();
        }
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPlatformHealthEndpoints();
            if (startup.Enabled)
            {
                endpoints.MapApplicationProxy();
            }
        });
        return app;
    }

    internal static WebApplication MapFeaturesEndpoints(this WebApplication app) => app.MapPlatformHealthEndpoints();
}
