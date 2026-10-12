using IdentityPlatform.AuthServer.Features.Accounts;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Protocol;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.AuthServer.Features.Pilot;

/// <summary>Composes the explicitly enabled Development pilot while preserving shared account isolation.</summary>
/// <example><code>services.AddFirstPartyPilot(credentials);</code></example>
public static class Setup
{
    /// <summary>Registers the complete standalone pilot; the caller retains credential ownership until host disposal.</summary>
    /// <example><code>services.AddFirstPartyPilot(credentials);</code></example>
    public static IServiceCollection AddFirstPartyPilot(this IServiceCollection services, IssuerCredentials credentials) =>
        AddFirstPartyPilot(services, credentials, administrator: false);

    /// <summary>Registers pilot services and the single shared account composition with an optional administrator boundary.</summary>
    internal static IServiceCollection AddFirstPartyPilot(this IServiceCollection services, IssuerCredentials credentials, bool administrator)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<PilotBootstrap>();
        services.AddScoped<PilotEligibility>();
        return services.AddAccounts(pilot: true, administrator).AddProtocol(credentials);
    }

    internal static async Task<int> InitializePilotAsync(this WebApplication app)
    {
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            PilotDatabaseBounds.Apply(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            await scope.ServiceProvider.GetRequiredService<PilotBootstrap>().InitializeAsync(deadline.Token);
            Console.WriteLine("First-party pilot initialization verified.");
            return 0;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("First-party pilot initialization failed. Existing state was not repaired or re-enabled.");
            return 2;
        }
    }
}
