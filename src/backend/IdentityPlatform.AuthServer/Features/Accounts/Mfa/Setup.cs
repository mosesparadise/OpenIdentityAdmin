using IdentityPlatform.Infrastructure.Features.Identity.Journeys;

namespace IdentityPlatform.AuthServer.Features.Accounts.Mfa;

/// <summary>Defines explicit composition for the mfa Razor feature.</summary>
internal static class Setup
{
    /// <summary>Keeps feature registration explicit; Razor discovers handlers through the exact account route convention.</summary>
    internal static IServiceCollection AddMfa(this IServiceCollection services) => services
        .AddScoped<JourneyPassword>()
        .AddScoped<JourneyCompletion>();
}
