using IdentityPlatform.Admin.Api.Features.ApiContracts;
using IdentityPlatform.Admin.Api.Features.Storage;
using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Protection;
using IdentityPlatform.ServiceDefaults;

namespace IdentityPlatform.Admin.Api.Features;

internal static class Setup
{
    /// <summary>Keeps management dormant unless the validated disposable Development profile explicitly activates it.</summary>
    internal static WebApplicationBuilder AddFeatures(this WebApplicationBuilder builder)
    {
        builder.Services.AddFeatures(builder.Configuration, builder.Environment);
        return builder;
    }

    /// <summary>Registers explicit generic hosting with the same disabled-by-default resource configuration.</summary>
    internal static IServiceCollection AddFeatures(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddStorage(configuration).AddApiContracts(configuration);
        if (LocalDemoConfiguration.IsEnabled(configuration, environment))
        {
            services.AddApplicationReads(new Uri(LocalDemoProfile.Issuer), LocalDemoProfile.Callback);
        }
        return services;
    }

    /// <summary>Maps route-owned native authentication only after explicit routing; generic hosting inserts no ambient authentication middleware.</summary>
    internal static IApplicationBuilder MapFeaturesEndpoints(this IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPlatformHealthEndpoints();
            if (app.ApplicationServices.GetService<ApiResourceProfile>() is not null)
            {
                endpoints.MapApplicationList();
            }
        });
        return app;
    }

    /// <summary>Maps the explicitly composed collection alongside health without activating ordinary production startup.</summary>
    internal static WebApplication MapFeaturesEndpoints(this WebApplication app)
    {
        if (app.Services.GetService<ApiResourceProfile>() is not null)
        {
            app.MapApplicationList();
        }
        return app.MapPlatformHealthEndpoints();
    }
}
