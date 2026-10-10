using IdentityPlatform.AuthServer.Features.Accounts;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Protocol;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.AuthServer.Features.Pilot;

public static class Setup
{
    public static IServiceCollection AddFirstPartyPilot(this IServiceCollection services, IssuerCredentials credentials)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<PilotBootstrap>();
        services.AddScoped<PilotEligibility>();
        return services.AddAccounts().AddPilotProtection().AddProtocol(credentials);
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
