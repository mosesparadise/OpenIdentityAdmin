using IdentityPlatform.Infrastructure.Features.Identity.Journeys;

namespace IdentityPlatform.AuthServer.Features.Accounts.Recovery;

/// <summary>Registers only the approved native restricted recovery feature.</summary>
internal static class Setup
{
    /// <summary>Uses shared account and transaction mechanisms without adding another authentication scheme.</summary>
    internal static IServiceCollection AddRecovery(this IServiceCollection services) => services.AddScoped<JourneyTransition>();
}
