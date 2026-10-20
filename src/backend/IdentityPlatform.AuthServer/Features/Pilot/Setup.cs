using IdentityPlatform.AuthServer.Features.Accounts;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Protocol;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.AuthServer.Features.Pilot;

/// <summary>Composes the explicitly enabled Development pilot while preserving shared account isolation.</summary>
/// <example><code>services.AddFirstPartyPilot(credentials);</code></example>
public static class Setup
{
    private const int InitializationMinutes = 1;
    private const int InitializationFailureCode = 2;
    private const string Initialized = "First-party pilot initialization verified.";
    private const string InitializationFailed = "First-party pilot initialization failed. Existing state was not repaired or re-enabled.";
    /// <summary>Registers the complete standalone pilot; the caller retains credential ownership until host disposal.</summary>
    /// <example><code>services.AddFirstPartyPilot(credentials);</code></example>
    public static IServiceCollection AddFirstPartyPilot(this IServiceCollection services, IssuerCredentials credentials) =>
        AddFirstPartyPilot(services, credentials, administrator: false);

    /// <summary>Registers pilot services and the single shared account composition with an optional administrator boundary.</summary>
    internal static IServiceCollection AddFirstPartyPilot(this IServiceCollection services, IssuerCredentials credentials, bool administrator)
    {
        return services.AddPilotServices().AddAccounts(pilot: true, administrator).AddProtocol(credentials);
    }

    /// <summary>Registers pilot application services independently from the shared account/native server composition.</summary>
    internal static IServiceCollection AddPilotServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<PilotBootstrap>();
        services.AddScoped<PilotEligibility>();
        return services;
    }

    /// <summary>Selects the explicit one-shot pilot scope before bootstrap can resolve any native manager.</summary>
    internal static async Task<int> InitializePilotAsync(this WebApplication app)
    {
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            scope.ServiceProvider.GetService<ProtocolStorageSelection>()?.Select(ProtocolStorageMode.LegacyPilot);
            PilotDatabaseBounds.Apply(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(InitializationMinutes));
            await scope.ServiceProvider.GetRequiredService<PilotBootstrap>().InitializeAsync(deadline.Token);
            Console.WriteLine(Initialized);
            return 0;
        }
        catch (Exception)
        {
            Console.Error.WriteLine(InitializationFailed);
            return InitializationFailureCode;
        }
    }
}
