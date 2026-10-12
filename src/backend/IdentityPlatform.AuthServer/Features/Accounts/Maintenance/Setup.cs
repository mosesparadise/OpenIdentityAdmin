using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

namespace IdentityPlatform.AuthServer.Features.Accounts.Maintenance;

/// <summary>Opts only enabled administrator hosts into bounded metadata maintenance.</summary>
internal static class Setup
{
    /// <summary>Registers the shared transaction mechanisms and one host-owned cleanup scheduler.</summary>
    internal static IServiceCollection AddAdministratorMaintenance(this IServiceCollection services)
    {
        services.AddAdministratorCeremonies();
        services.AddHostedService<AdministratorCleanupService>();
        return services;
    }
}
