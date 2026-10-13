using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IdentityPlatform.Infrastructure.Features.Identity.Governance;

/// <summary>Composes internal governance mechanisms without mapping management endpoints or activating external access.</summary>
internal static class Setup
{
    /// <summary>Registers one scoped native transaction graph after persistence, factors, ceremonies and journey protections.</summary>
    internal static IServiceCollection AddAdministratorGovernance(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<RoleManager<IdentityRole>, GovernanceRoleManager>());
        services.TryAddScoped<JourneyRevocation>();
        return services.AddScoped<GovernanceGrants>().AddScoped<GovernanceDiscovery>().AddScoped<GovernanceUsability>()
            .AddScoped<GovernanceSurvival>().AddScoped<GovernanceMutation>().AddScoped<GovernanceService>();
    }
}
