using System.Diagnostics;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace IdentityPlatform.Persistence.Tests.Features;

public sealed class DatabaseFailureTests
{
    [Theory]
    [InlineData(DatabaseProvider.PostgreSql, Traits = ["Provider", "PostgreSql"])]
    [InlineData(DatabaseProvider.SqlServer, Traits = ["Provider", "SqlServer"])]
    public async Task Partial_initialization_can_resume_and_outages_fail_safely_within_deadline(DatabaseProvider provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        var token = deadline.Token;
        await using var postgres = new PostgreSqlBuilder(
            "postgres:18@sha256:74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336").Build();
        await using var sql = new MsSqlBuilder(
            "mcr.microsoft.com/mssql/server:2022-latest@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090").Build();
        if (provider == DatabaseProvider.PostgreSql)
        {
            await postgres.StartAsync(token);
        }
        else
        {
            await sql.StartAsync(token);
        }

        var connection = provider == DatabaseProvider.PostgreSql ? postgres.GetConnectionString() : sql.GetConnectionString();
        var broken = provider == DatabaseProvider.PostgreSql
            ? new NpgsqlConnectionStringBuilder(connection) { Port = 1, Timeout = 1, Password = "synthetic-secret" }.ConnectionString
            : new SqlConnectionStringBuilder(connection) { DataSource = "127.0.0.1,1", ConnectTimeout = 1, Password = "synthetic-secret" }.ConnectionString;
        var logs = new CapturedLogs();
        await using var partial = CreateServices(connection, broken);
        var initialHealth = new SchemaReadinessCheck<PlatformDbContext>(partial, SchemaCatalog.Platform(provider));
        Assert.Equal(HealthStatus.Unhealthy, (await initialHealth.CheckHealthAsync(new(), token)).Status);
        Assert.Equal(1, await DatabaseUpgrade.RunAsync(partial, token));
        await using (var scope = partial.CreateAsyncScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            Assert.Equal(SchemaCatalog.Platform(provider), await platform.Database.GetAppliedMigrationsAsync(token));
        }
        await using var recovered = CreateServices(connection, connection);
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(recovered, token));
        Assert.Equal(0, await DatabaseUpgrade.RunAsync(recovered, token));
        Assert.Equal(1, await DatabaseUpgrade.RunAsync(recovered, new CancellationToken(canceled: true)));
        Assert.DoesNotContain(logs.Messages, message => message.Contains("synthetic-secret") || message.Contains(broken));
        Assert.Contains(logs.Messages, message => message.StartsWith("Migration failed (", StringComparison.Ordinal));

        if (provider == DatabaseProvider.PostgreSql)
        {
            await postgres.StopAsync(token);
        }
        else
        {
            await sql.StopAsync(token);
        }

        var clock = Stopwatch.StartNew();
        var health = await new SchemaReadinessCheck<PlatformDbContext>(recovered, SchemaCatalog.Platform(provider))
            .CheckHealthAsync(new(), token);
        Assert.Equal(HealthStatus.Unhealthy, health.Status);
        Assert.Null(health.Exception);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(8), "Readiness must fail within its bounded deadline.");

        ServiceProvider CreateServices(string platform, string sessions) => new ServiceCollection()
            .AddLogging(builder => builder.AddProvider(logs).AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None))
            .AddPlatformPersistence(provider, platform, sessions).BuildServiceProvider(validateScopes: true);
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public List<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new Capture(Messages);
        public void Dispose()
        {
        }
        private sealed class Capture(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Add(formatter(state, exception));
        }
    }
}
