using IdentityPlatform.AppHost.Features;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.AppHost.Composition;

internal static class DistributedApplicationExtensions
{
    internal static IDistributedApplicationBuilder AddConfigurations(this IDistributedApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("This AppHost is for local Development only. Production provisioning is external.");
        }
        return builder;
    }

    internal static IDistributedApplicationBuilder ConfigureServices(this IDistributedApplicationBuilder builder) => builder.AddFeatures();
}
