using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.AdministratorProtocol.Upgrade;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Migrator.Features.AdministratorProtocol;

/// <summary>Exposes initialization only through a separately selected one-shot Migrator operation.</summary>
internal static class Setup
{
    private const int InputDeadlineSeconds = 30;
    private const int InvalidInputCode = 2;
    private const string Upgraded = "Administrator browser registration upgraded.";
    private const string Reconciled = "Administrator browser registration upgrade already applied.";
    private const string InvalidUpgrade = "Invalid administrator browser registration upgrade input.";
    private const string FailedUpgrade = "Administrator browser registration upgrade failed. Check restricted operational diagnostics.";
    private const string Created = "Administrator protocol registration initialized.";
    private const string Unchanged = "Administrator protocol registration already initialized.";
    private const string Invalid = "Invalid administrator protocol initialization input.";
    private const string Failed = "Administrator protocol initialization failed. Check restricted operational diagnostics.";

    /// <summary>Uses native managers with entity caching disabled and the same strict protocol transaction owner.</summary>
    internal static IServiceCollection AddAdministratorProtocolInitialization(this IServiceCollection services) => services.AddProtocolStorageOwnership();

    /// <summary>Requires redirected public input and a current Platform schema; success follows actual commit and cleanup.</summary>
    internal static Task<int> InitializeAdministratorProtocolAsync(this IHost host, IRelationalDatabaseProvider provider) => RunAsync(host, provider, false);

    /// <summary>Runs the separately selected audited upgrade without selecting B3 in any live issuer host.</summary>
    internal static Task<int> UpgradeAdministratorBrowserRegistrationAsync(this IHost host, IRelationalDatabaseProvider provider) => RunAsync(host, provider, true);

    /// <summary>Shares bounded redirected input, current-schema admission and safe output after actual owned cleanup.</summary>
    private static async Task<int> RunAsync(IHost host, IRelationalDatabaseProvider provider, bool upgrade)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(InputDeadlineSeconds));
        try
        {
            if (!Console.IsInputRedirected)
            {
                throw new ArgumentException(Invalid);
            }
            using var stream = Console.OpenStandardInput();
            var input = await (upgrade ? AdministratorRegistrationInput.ReadUpgradeAsync(stream, deadline.Token) :
                AdministratorRegistrationInput.ReadAsync(stream, deadline.Token)).WaitAsync(deadline.Token);
            var health = await new SchemaReadinessCheck<PlatformDbContext>(host.Services, SchemaCatalog.Platform(provider))
                .CheckHealthAsync(new HealthCheckContext(), deadline.Token);
            if (health.Status != HealthStatus.Healthy)
            {
                throw new InvalidOperationException(Failed);
            }
            var changed = upgrade ? await AdministratorRegistrationUpgrade.RunAsync(host.Services, input, deadline.Token) :
                await AdministratorRegistrationInitializer.RunAsync(host.Services, input.Policy, input.OperatorReference, input.OperationId, deadline.Token);
            Console.WriteLine(upgrade ? (changed ? Upgraded : Reconciled) : (changed ? Created : Unchanged));
            return 0;
        }
        catch (ArgumentException) { Console.Error.WriteLine(upgrade ? InvalidUpgrade : Invalid); return InvalidInputCode; }
        catch (Exception error) when (error is not OutOfMemoryException) { Console.Error.WriteLine(upgrade ? FailedUpgrade : Failed); return 1; }
    }
}
