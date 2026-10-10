using IdentityPlatform.AppHost.Features.Database;
using IdentityPlatform.AppHost.Features.Pilot;
using IdentityPlatform.AppHost.Features.Services;

namespace IdentityPlatform.AppHost.Features;

internal static class Setup
{
    internal static IDistributedApplicationBuilder AddFeatures(this IDistributedApplicationBuilder builder)
    {
        var database = builder.AddDatabase();
        var services = builder.AddServices(database);
        builder.AddPilot(services.Auth, services.Migrator, database.PlatformRuntime, database.Provider);
        return builder;
    }
}
