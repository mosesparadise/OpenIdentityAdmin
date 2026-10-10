using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Npgsql;

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
            var connection = database.GetDbConnection();
            // History APIs can probe database existence using provider-specific retry and
            // connection policies. Readiness needs one bounded, read-only history query.
            var (schema, table) = typeof(TContext) == typeof(PlatformDbContext)
                ? ("platform", "PlatformMigrations")
                : typeof(TContext) == typeof(AdminSessionDbContext)
                    ? ("bff", "SessionMigrations")
                    : throw new InvalidOperationException("Unsupported readiness context.");
            var sqlServer = connection is SqlConnection;
            connection.ConnectionString = connection switch
            {
                SqlConnection => new SqlConnectionStringBuilder(connection.ConnectionString)
                    { ConnectTimeout = 3, ConnectRetryCount = 0, Pooling = false }.ConnectionString,
                NpgsqlConnection => new NpgsqlConnectionStringBuilder(connection.ConnectionString)
                    { Timeout = 3, Pooling = false }.ConnectionString,
                _ => throw new InvalidOperationException("Unsupported readiness provider.")
            };
            await connection.OpenAsync(deadline.Token);
            await using var command = connection.CreateCommand();
            command.CommandTimeout = 3;
            // Identifiers come only from the fixed context mapping above.
            command.CommandText = sqlServer
                ? $"SELECT [MigrationId] FROM [{schema}].[{table}] ORDER BY [MigrationId]"
                : $"SELECT \"MigrationId\" FROM \"{schema}\".\"{table}\" ORDER BY \"MigrationId\"";
            var applied = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(deadline.Token);
            while (await reader.ReadAsync(deadline.Token)) applied.Add(reader.GetString(0));
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
