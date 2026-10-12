using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Migrator.Features.Bootstrap.Initialize;

public static class Setup
{
    public static IServiceCollection AddAdministratorBootstrap(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System).AddScoped<AdministratorBootstrap>().AddScoped<BootstrapCancellation>();
        services.AddIdentityCore<PlatformUser>().AddUserManager<BootstrapUserManager>()
            .AddRoles<IdentityRole>().AddRoleManager<BootstrapRoleManager>();
        services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequiredLength = 16;
            options.Password.RequiredUniqueChars = 1;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
        });
        services.Configure<PasswordHasherOptions>(options =>
        {
            options.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
            options.IterationCount = 210_000;
        });
        return services;
    }

    internal static async Task<int> InitializeAdministratorAsync(this IHost host, DatabaseProvider provider)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            if (!Console.IsInputRedirected)
            {
                throw new ArgumentException("Redirected input required.");
            }
            using var inputStream = Console.OpenStandardInput();
            // Console pipe reads may not honor cancellation once blocked. Bound the owning
            // command wait as well; process exit terminates any outstanding background read.
            var input = await BootstrapInput.ReadAsync(inputStream, deadline.Token).WaitAsync(deadline.Token);
            var health = await new SchemaReadinessCheck<PlatformDbContext>(host.Services, SchemaCatalog.Platform(provider))
                .CheckHealthAsync(new HealthCheckContext(), deadline.Token);
            if (health.Status != HealthStatus.Healthy)
            {
                throw new InvalidOperationException("Schema is not current.");
            }
            await using var scope = host.Services.CreateAsyncScope();
            var outcome = await scope.ServiceProvider.GetRequiredService<AdministratorBootstrap>().InitializeAsync(input, deadline.Token);
            Console.WriteLine(outcome == BootstrapOutcome.Created ? "Administrator enrollment initialized." : "Administrator bootstrap already initialized.");
            return 0;
        }
        catch (ArgumentException)
        {
            Console.Error.WriteLine("Invalid administrator bootstrap input.");
            return 2;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Console.Error.WriteLine("Administrator bootstrap failed. Check restricted operational diagnostics.");
            return 1;
        }
    }
}
