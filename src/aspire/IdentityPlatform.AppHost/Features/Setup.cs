using IdentityPlatform.AppHost.Features.Accounts;
using IdentityPlatform.AppHost.Features.Database;
using IdentityPlatform.AppHost.Features.Pilot;
using IdentityPlatform.AppHost.Features.Services;

namespace IdentityPlatform.AppHost.Features;

internal static class Setup
{
    /// <summary>Builds database and service resources, then explicitly composes independent account and pilot activation.</summary>
    internal static IDistributedApplicationBuilder AddFeatures(this IDistributedApplicationBuilder builder)
    {
        var database = builder.AddDatabase();
        var services = builder.AddServices(database);
        var protection = new IssuerProtection(builder, services.Auth);
        builder.AddAccounts(services.Auth, protection);
        builder.AddPilot(services.Auth, services.Migrator, database.PlatformRuntime, database.Provider, protection);
        return builder;
    }
}
