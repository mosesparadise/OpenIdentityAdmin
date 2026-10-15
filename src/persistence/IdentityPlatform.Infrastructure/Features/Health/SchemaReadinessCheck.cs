using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityPlatform.Infrastructure.Features.Health;

/// <summary>Checks exact schema history with one isolated bounded read and safe failure reports.</summary>
/// <example><code>new SchemaReadinessCheck&lt;PlatformDbContext&gt;(services, provider.Migrations(DatabaseStore.Platform));</code></example>
public sealed class SchemaReadinessCheck<TContext>(IServiceProvider services,
    IReadOnlyList<string> expectedMigrations) : IHealthCheck where TContext : DbContext
{
    private const string UnsupportedContext = "Unsupported readiness context.";
    private const string IncompatibleSchema = "Schema is not compatible.";
    private const string UnavailableStorage = "Required storage is unavailable.";
    private const int DeadlineSeconds = 5;
    private const int QuerySeconds = 3;

    /// <summary>Returns healthy only when the complete ordered history equals the required migration catalog.</summary>
    /// <example><code>var result = await check.CheckHealthAsync(context, cancellationToken);</code></example>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(DeadlineSeconds));
        try
        {
            await using var scope = services.CreateAsyncScope();
            var storage = scope.ServiceProvider.GetRequiredService<TContext>();
            var database = storage.Database;
            var connection = database.GetDbConnection();
            // History APIs can probe database existence using provider-specific retry and
            // connection policies. Readiness needs one bounded, read-only history query.
            var store = typeof(TContext) == typeof(PlatformDbContext) ? DatabaseStore.Platform
                : typeof(TContext) == typeof(AdminSessionDbContext) ? DatabaseStore.Sessions
                : throw new InvalidOperationException(UnsupportedContext);
            var policy = storage.GetDatabaseProvider().Connections;
            policy.Bound(connection, seconds: QuerySeconds, readiness: true);
            await connection.OpenAsync(deadline.Token);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = QuerySeconds;
            // Identifiers come only from the fixed context mapping above.
            command.CommandText = policy.HistoryQuery(store);
            var applied = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(deadline.Token);
            while (await reader.ReadAsync(deadline.Token))
            {
                applied.Add(reader.GetString(0));
            }

            return applied.SequenceEqual(expectedMigrations, StringComparer.Ordinal)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy(IncompatibleSchema);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Never attach provider exceptions to health reports, logs or HTTP responses.
            return HealthCheckResult.Unhealthy(UnavailableStorage);
        }
    }
}
