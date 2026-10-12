using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;

/// <summary>Registers administrator transaction mechanisms without mapping pages or granting authentication authority.</summary>
/// <example><code>services.AddAdministratorCeremonies(); // Requires persistence and protected native factor registrations.</code></example>
public static class Setup
{
    /// <summary>Installs scoped policies sharing the native context and clock; no background service or key ring is created here.</summary>
    /// <example><code>services.AddAdministratorFactors(issuer).AddAdministratorCeremonies();</code></example>
    public static IServiceCollection AddAdministratorCeremonies(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<CeremonyClock>();
        services.AddScoped<CeremonyBudget>();
        services.AddScoped<CeremonyAdmission>();
        services.AddScoped<CeremonyGenerations>();
        services.AddScoped<CeremonyReplay>();
        services.AddScoped<CeremonyCleanup>();
        return services;
    }
}
