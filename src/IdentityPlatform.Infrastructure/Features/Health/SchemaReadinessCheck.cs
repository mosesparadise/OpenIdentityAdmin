using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityPlatform.Infrastructure.Features.Health;

public sealed class SchemaReadinessCheck<TContext>(IServiceProvider services,
    IReadOnlyList<string> expectedMigrations) : IHealthCheck where TContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await using var scope = services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<TContext>().Database;
            database.SetCommandTimeout(3);
            var applied = await database.GetAppliedMigrationsAsync(deadline.Token);
            return applied.SequenceEqual(expectedMigrations, StringComparer.Ordinal)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Schema is not compatible.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Never attach provider exceptions to health reports, logs or HTTP responses.
            return HealthCheckResult.Unhealthy("Required storage is unavailable.");
        }
    }
}
