using System.Data.Common;
using DotNet.Testcontainers.Containers;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;

/// <summary>Owns a real disposable provider and both stores for exact immediate-prior upgrade and relational constraint tests.</summary>
internal sealed class ManagedSchemaFixture(IContainer container, ServiceProvider services, CancellationTokenSource deadline) : IAsyncDisposable
{
    private const string Postgres = "PostgreSql";
    private const string PostgresImage = "postgres:18@sha256:74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336";
    private const string SqlImage = "mcr.microsoft.com/mssql/server:2022-latest@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090";
    private const string DatabaseName = "managed_applications_schema";
    private const string PriorPostgres = "20261026113929_BrowserAuditCorrelation";
    private const string PriorSql = "20261026114108_BrowserAuditCorrelation";
    private const int DeadlineMinutes = 3;
    internal IServiceProvider Services => services;
    internal CancellationToken Token => deadline.Token;

    /// <summary>Uses actual pinned engines and independently pinned previous Platform migrations; no SQLite or in-memory replacement is involved.</summary>
    internal static async Task<ManagedSchemaFixture> CreateAsync(string provider, bool prior = false)
    {
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(DeadlineMinutes));
        IContainer container = provider == Postgres ? new PostgreSqlBuilder(PostgresImage).WithDatabase(DatabaseName).Build() : new MsSqlBuilder(SqlImage).Build();
        ServiceProvider? services = null;
        try
        {
            await container.StartAsync(deadline.Token);
            var connection = container is PostgreSqlContainer postgres ? postgres.GetConnectionString() :
                new SqlConnectionStringBuilder(((MsSqlContainer)container).GetConnectionString()) { InitialCatalog = DatabaseName }.ConnectionString;
            services = new ServiceCollection().AddLogging(options => options.ClearProviders())
                .AddPlatformPersistence(TestDatabaseProviders.Resolve(provider), connection, connection).BuildServiceProvider(validateScopes: true);
            var result = new ManagedSchemaFixture(container, services, deadline);
            await result.MigrateAsync(prior ? provider == Postgres ? PriorPostgres : PriorSql : null);
            return result;
        }
        catch
        {
            if (services is not null)
            {
                await services.DisposeAsync();
            }
            await container.DisposeAsync();
            deadline.Dispose();
            throw;
        }
    }

    /// <summary>Targets native migrations only, retaining the unchanged Sessions history while exercising the requested Platform predecessor.</summary>
    private async Task MigrateAsync(string? target)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().GetService<IMigrator>().MigrateAsync(target, Token);
        await scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>().Database.MigrateAsync(Token);
    }

    /// <summary>Creates a fresh context so constraint failures or concurrency snapshots cannot contaminate another observation.</summary>
    internal AsyncServiceScope Scope() => services.CreateAsyncScope();

    /// <summary>Reuses the frozen physical snapshot implementation without changing its queries or captured fields.</summary>
    internal async Task<Dictionary<string, string[]>> SnapshotAsync(string provider)
    {
        await using var scope = Scope();
        DbConnection connection = scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Database.GetDbConnection();
        return await DatabaseSnapshot.CaptureRowsAsync(connection,
            provider == Postgres ? MigrationCompatibilityTests.PostgresIndexes : MigrationCompatibilityTests.SqlIndexes,
            provider == Postgres ? MigrationCompatibilityTests.PostgresChecks : MigrationCompatibilityTests.SqlChecks, Token);
    }

    /// <summary>Disposes native contexts and stops the selected real container before releasing the bounded test deadline.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await services.DisposeAsync();
        }
        finally
        {
            await container.DisposeAsync();
            deadline.Dispose();
        }
    }
}
