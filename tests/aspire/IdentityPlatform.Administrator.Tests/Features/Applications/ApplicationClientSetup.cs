using IdentityPlatform.Admin.Bff.Features.Applications;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using BffContracts = IdentityPlatform.Admin.Bff.Features.ApiContracts.Setup;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Adds only the explicitly selected real proxy to the existing HTTPS BFF fixture.</summary>
internal static class ApplicationClientSetup
{
    private const string TitleSetting = "ApiContracts:Metadata:Title";
    private const string DescriptionSetting = "ApiContracts:Metadata:Description";
    private const string Title = "Application BFF test";
    private const string Description = "Actual protected application proxy.";

    /// <summary>Supplies finite native metadata and a fixed trusted API destination without changing ordinary browser-session fixtures.</summary>
    internal static void Configure(IWebHostBuilder web, Uri? api)
    {
        if (api is null)
        {
            return;
        }
        web.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [TitleSetting] = Title,
            [DescriptionSetting] = Description
        }));
        web.ConfigureServices((context, services) =>
        {
            BffContracts.AddApiContracts(services, context.Configuration);
            services.AddApplicationProxy(api);
        });
    }

    /// <summary>Uses explicit routing after native browser middleware, preserving the existing generic-host authentication boundary.</summary>
    internal static void Use(IApplicationBuilder app, Uri? api)
    {
        if (api is null)
        {
            return;
        }
        app.UseRouting();
        app.UseEndpoints(routes => routes.MapApplicationProxy());
    }
}
