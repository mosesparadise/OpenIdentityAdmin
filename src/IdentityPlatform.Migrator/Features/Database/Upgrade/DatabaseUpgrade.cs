using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using IdentityPlatform.Migrator.Features.Database.LocalRoles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Migrator.Features.Database.Upgrade;

public static class DatabaseUpgrade
{
    public static async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken,
        LocalRuntimeCredentials? localCredentials = null)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseUpgrade");
        try
        {
            await scope.ServiceProvider.GetRequiredService<PlatformDbContext>()
                .Database.MigrateAsync(cancellationToken);
            await scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>()
                .Database.MigrateAsync(cancellationToken);
            if (localCredentials is not null)
            {
                await LocalRuntimeRoleProvisioner.ProvisionAsync(
                    scope.ServiceProvider.GetRequiredService<PlatformDbContext>(), RuntimeStore.Platform,
                    localCredentials.PlatformPassword, cancellationToken);
                await LocalRuntimeRoleProvisioner.ProvisionAsync(
                    scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>(), RuntimeStore.Sessions,
                    localCredentials.SessionsPassword, cancellationToken);
            }
            logger.LogInformation("Platform and admin session schemas are current.");
            return 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Provider exception messages can include private server/connection details.
            logger.LogError("Migration failed ({FailureType}). Check restricted database diagnostics.",
                exception.GetType().Name);
            return 1;
        }
    }
}
