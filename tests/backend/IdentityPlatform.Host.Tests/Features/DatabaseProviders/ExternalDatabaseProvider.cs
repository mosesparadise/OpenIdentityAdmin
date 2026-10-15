using IdentityPlatform.Database;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Host.Tests.Features.DatabaseProviders;

/// <summary>Implements only public contracts outside shipped provider assemblies; this is a boundary control, not a third supported engine.</summary>
internal sealed class ExternalDatabaseProvider(string identifier, string? collation = null) : IRelationalDatabaseProvider
{
    private const string Driver = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private const string Connection = "Host=unused;Database=external_contract";
    private const string MigrationsAssembly = "IdentityPlatform.Migrations.PostgreSql";
    private const int CommandSeconds = 30;
    public string Id => identifier;
    public string EfProviderName => Driver;
    public string DesignTimeConnection => Connection;
    public IDatabaseModelDialect Model { get; } = new ExternalModelDialect(collation);
    public IDatabaseConnectionPolicy Connections { get; } = new ExternalConnectionPolicy();
    public ILocalDatabaseProvisioner? LocalProvisioning => null;

    /// <summary>Configures the existing driver directly without inheriting or delegating to a built-in provider module.</summary>
    public void Configure(DbContextOptionsBuilder options, string connection, DatabaseStore store) =>
        options.UseNpgsql(connection, database => database.MigrationsAssembly(MigrationsAssembly)
            .MigrationsHistoryTable(DatabaseStores.History(store), DatabaseStores.Schema(store)).CommandTimeout(CommandSeconds));

    /// <summary>This model/registration boundary control does not claim a migrated database or publish a compatibility catalog.</summary>
    public IReadOnlyList<string> Migrations(DatabaseStore store) => [];
}
