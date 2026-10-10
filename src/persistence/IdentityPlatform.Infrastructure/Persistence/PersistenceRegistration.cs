using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IdentityPlatform.Infrastructure.Persistence;

public static class PersistenceRegistration
{
    public static IServiceCollection AddPlatformPersistence(
        this IServiceCollection services, DatabaseProvider provider,
        string platformConnection, string sessionConnection)
    {
        return services.AddIdentityPersistence(provider, platformConnection)
            .AddSessionPersistence(provider, sessionConnection);
    }

    public static IServiceCollection AddIdentityPersistence(
        this IServiceCollection services, DatabaseProvider provider, string platformConnection)
    {
        ValidateProviderConnection(platformConnection, provider);

        services.AddDbContext<PlatformDbContext>(options =>
            ConfigureDatabase(options, provider, platformConnection, "PlatformMigrations"));

        services.AddIdentityCore<PlatformUser>(IdentityStoreConfiguration.Configure)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<PlatformDbContext>();

        services.AddOpenIddict().AddCore(options =>
        {
            options.UseEntityFrameworkCore().UseDbContext<PlatformDbContext>();
            // Separate processes must see current administrative state; no stale entity caches.
            options.DisableEntityCaching();
        });

        return services;
    }

    public static IServiceCollection AddSessionPersistence(
        this IServiceCollection services, DatabaseProvider provider, string sessionConnection)
    {
        ValidateProviderConnection(sessionConnection, provider);
        services.AddDbContext<AdminSessionDbContext>(options =>
            ConfigureDatabase(options, provider, sessionConnection, "SessionMigrations"));
        return services;
    }

    public static void ConfigureDatabase(DbContextOptionsBuilder options, DatabaseProvider provider,
        string connection, string historyTable)
    {
        ValidateConnection(connection, "ConnectionStrings");
        switch (provider)
        {
            case DatabaseProvider.PostgreSql:
                options.UseNpgsql(connection, database => database
                    .MigrationsAssembly("IdentityPlatform.Migrations.PostgreSql")
                    .MigrationsHistoryTable(historyTable, historyTable == "SessionMigrations" ? "bff" : "platform")
                    .CommandTimeout(30));
                break;
            case DatabaseProvider.SqlServer:
                options.UseSqlServer(connection, database => database
                    .MigrationsAssembly("IdentityPlatform.Migrations.SqlServer")
                    .MigrationsHistoryTable(historyTable, historyTable == "SessionMigrations" ? "bff" : "platform")
                    .CommandTimeout(30));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider));
        }

        options.UseOpenIddict();
        // Deliberately no sensitive-data logging or implicit execution-strategy retries.
    }

    private static void ValidateProviderConnection(string connection, DatabaseProvider provider)
    {
        ValidateConnection(connection, "ConnectionStrings");
        try
        {
            var valid = provider switch
            {
                DatabaseProvider.PostgreSql => new NpgsqlConnectionStringBuilder(connection) is var value &&
                    !string.IsNullOrWhiteSpace(value.Host) && !string.IsNullOrWhiteSpace(value.Database),
                DatabaseProvider.SqlServer => new SqlConnectionStringBuilder(connection) is var value &&
                    !string.IsNullOrWhiteSpace(value.DataSource) && !string.IsNullOrWhiteSpace(value.InitialCatalog),
                _ => false
            };
            if (valid)
            {
                return;
            }
        }
        catch (ArgumentException) { /* Provider messages can echo the supplied setting. */ }
        throw new ArgumentException("Invalid database connection configuration. An explicit server and database are required.");
    }

    private static void ValidateConnection(string connection, string setting)
    {
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new ArgumentException($"{setting} is required.");
        }
    }
}
