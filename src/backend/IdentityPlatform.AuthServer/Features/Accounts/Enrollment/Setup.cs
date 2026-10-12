using IdentityPlatform.Infrastructure.Features.Identity.Journeys;

namespace IdentityPlatform.AuthServer.Features.Accounts.Enrollment;

/// <summary>Defines explicit composition for the enrollment Razor feature.</summary>
internal static class Setup
{
    /// <summary>Keeps feature registration explicit; Razor discovers handlers through the exact account route convention.</summary>
    internal static IServiceCollection AddEnrollment(this IServiceCollection services) => services
        .AddScoped<JourneyActivation>()
        .AddScoped<JourneyStaging>()
        .AddScoped<JourneyRevocation>();
}
