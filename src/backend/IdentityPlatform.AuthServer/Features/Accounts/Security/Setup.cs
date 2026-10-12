using IdentityPlatform.Infrastructure.Features.Identity.Journeys;

namespace IdentityPlatform.AuthServer.Features.Accounts.Security;

/// <summary>Defines explicit composition for the security Razor feature.</summary>
internal static class Setup
{
    /// <summary>Keeps feature registration explicit; Razor discovers handlers through the exact account route convention.</summary>
    internal static IServiceCollection AddAccountSecurity(this IServiceCollection services) => services
        .AddScoped<JourneySessions>().AddScoped<JourneyLogout>();
}
