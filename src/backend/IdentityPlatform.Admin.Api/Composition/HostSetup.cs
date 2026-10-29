using IdentityPlatform.Admin.Api.Features;
using IdentityPlatform.ServiceDefaults;

namespace IdentityPlatform.Admin.Api.Composition;

/// <summary>Composes the API without minimal hosting's automatic authentication before route-owned deadlines.</summary>
internal static class HostSetup
{
    /// <summary>Preserves framework configuration and telemetry while explicitly owning all authentication dispatch.</summary>
    internal static IHostBuilder Create(string[] args) => Host.CreateDefaultBuilder(args).AddPlatformServiceDefaults()
        .ConfigureWebHostDefaults(web => web.ConfigureServices((context, services) =>
            services.AddFeatures(context.Configuration, context.HostingEnvironment)).Configure(app => app.MapFeaturesEndpoints()));
}
