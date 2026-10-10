using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.LocalRoles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Migrator.Features.Database.Upgrade;

public static class DatabaseUpgrade
{
    private static readonly Action<ILogger, Exception?> SchemasCurrent = LoggerMessage.Define(
        LogLevel.Information, new EventId(1001, nameof(SchemasCurrent)),
        "Platform and admin session schemas are current.");
    private static readonly Action<ILogger, string, Exception?> MigrationFailed = LoggerMessage.Define<string>(
        LogLevel.Error, new EventId(1002, nameof(MigrationFailed)),
        "Migration failed ({FailureType}). Check restricted database diagnostics.");

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
            SchemasCurrent(logger, null);
            return 0;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Provider exception messages can include private server/connection details.
            MigrationFailed(logger, exception.GetType().Name, null);
            return 1;
        }
    }
}
