using IdentityPlatform.Database;
using IdentityPlatform.Migrator.Composition;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Migrator.Features.Bootstrap.Initialize;

/// <summary>Composes and runs the explicitly requested administrator bootstrap operation.</summary>
/// <example><code>services.AddAdministratorBootstrap();</code></example>
public static class Setup
{
    private const string RedirectRequired = "Redirected input required.";
    private const string SchemaRequired = "Schema is not current.";
    private const string CreatedMessage = "Administrator enrollment initialized.";
    private const string AlreadyInitialized = "Administrator bootstrap already initialized.";
    private const string InvalidInput = "Invalid administrator bootstrap input.";
    private const string FailedMessage = "Administrator bootstrap failed. Check restricted operational diagnostics.";
    private const int DeadlineMinutes = 3;
    private const int InvalidInputExitCode = 2;

    /// <summary>Registers manager-owned bootstrap state with the established password and hashing policy.</summary>
    /// <example><code>services.AddAdministratorBootstrap();</code></example>
    public static IServiceCollection AddAdministratorBootstrap(this IServiceCollection services)
    {
        const int PasswordLength = 16;
        const int HashIterations = 210_000;
        services.AddSingleton(TimeProvider.System).AddScoped<AdministratorBootstrap>().AddScoped<BootstrapCancellation>();
        services.AddIdentityCore<PlatformUser>().AddUserManager<BootstrapUserManager>()
            .AddRoles<IdentityRole>().AddRoleManager<BootstrapRoleManager>();
        services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequiredLength = PasswordLength;
            options.Password.RequiredUniqueChars = 1;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
        });
        services.Configure<PasswordHasherOptions>(options =>
        {
            options.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
            options.IterationCount = HashIterations;
        });
        return services;
    }

    /// <summary>Requires redirected bounded input and the selected provider's current schema before initializing authority.</summary>
    internal static async Task<int> InitializeAdministratorAsync(this IHost host, IRelationalDatabaseProvider provider)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(DeadlineMinutes));
        try
        {
            if (!Console.IsInputRedirected)
            {
                throw new ArgumentException(RedirectRequired);
            }
            using var inputStream = Console.OpenStandardInput();
            // Console pipe reads may not honor cancellation once blocked. Bound the owning
            // command wait as well; process exit terminates any outstanding background read.
            var input = await BootstrapInput.ReadAsync(inputStream, deadline.Token).WaitAsync(deadline.Token);
            var health = await new SchemaReadinessCheck<PlatformDbContext>(host.Services, SchemaCatalog.Platform(provider))
                .CheckHealthAsync(new HealthCheckContext(), deadline.Token);
            if (health.Status != HealthStatus.Healthy)
            {
                throw new InvalidOperationException(SchemaRequired);
            }
            await using var scope = host.Services.CreateAsyncScope();
            var outcome = await scope.ServiceProvider.GetRequiredService<AdministratorBootstrap>().InitializeAsync(input, deadline.Token);
            Console.WriteLine(outcome == BootstrapOutcome.Created ? CreatedMessage : AlreadyInitialized);
            return 0;
        }
        catch (ArgumentException)
        {
            Console.Error.WriteLine(InvalidInput);
            return InvalidInputExitCode;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine(FailedMessage);
            return 1;
        }
    }
}
