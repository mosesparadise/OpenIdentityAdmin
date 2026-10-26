using IdentityPlatform.Admin.Bff.Features;
using IdentityPlatform.Admin.Bff.Features.LocalDemo;
using IdentityPlatform.ServiceDefaults;

namespace IdentityPlatform.Admin.Bff.Composition;

/// <summary>Composes one explicit BFF web host for dormant and local demo modes without automatic authentication middleware.</summary>
internal static class HostSetup
{
    /// <summary>Preserves framework configuration precedence, safe telemetry and caller-owned credential lifetime.</summary>
    internal static IHostBuilder Create(string[] args, StartupContext startup) => Host.CreateDefaultBuilder(args)
        .AddPlatformServiceDefaults()
        .ConfigureWebHostDefaults(web => web.ConfigureServices((context, services) =>
        {
            services.AddFeatures(context.Configuration);
            startup.Configure(services, context.Configuration, context.HostingEnvironment);
        }).Configure(app => app.MapFeaturesEndpoints(startup)));
}
