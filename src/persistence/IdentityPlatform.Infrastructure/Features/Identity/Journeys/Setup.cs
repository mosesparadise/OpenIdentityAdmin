using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Registers account journey mechanisms only for explicitly enabled administrator hosts.</summary>
internal static class Setup
{
    /// <summary>Shares native transaction dependencies without exposing unrestricted factor setters or session factories.</summary>
    internal static IServiceCollection AddAdministratorJourneys(this IServiceCollection services) => services
        .AddScoped<JourneyLocator>()
        .AddScoped<JourneyEligibility>()
        .AddScoped<JourneyDenial>();
}
