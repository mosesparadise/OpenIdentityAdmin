using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IdentityPlatform.Infrastructure.Persistence;

/// <summary>Registers provider-specific native Identity/OpenIddict persistence with unavailable factors by default.</summary>
/// <example><code>services.AddIdentityPersistence(provider, runtimeConnection);</code></example>
public static class PersistenceRegistration
{
    /// <summary>Composes both logical stores using one explicit provider registration.</summary>
    /// <example><code>services.AddPlatformPersistence(provider, platformConnection, sessionConnection);</code></example>
    public static IServiceCollection AddPlatformPersistence(
        this IServiceCollection services, IRelationalDatabaseProvider provider,
        string platformConnection, string sessionConnection)
    {
        return services.AddIdentityPersistence(provider, platformConnection)
            .AddSessionPersistence(provider, sessionConnection);
    }

    /// <summary>Registers native managers and a guarded factor store using the supplied least-privilege database connection.</summary>
    /// <example><code>services.AddIdentityPersistence(provider, configuredRuntimeConnection);</code></example>
    public static IServiceCollection AddIdentityPersistence(
        this IServiceCollection services, IRelationalDatabaseProvider provider, string platformConnection)
    {
        const string PlatformHistory = "PlatformMigrations";
        provider.Connections.Validate(platformConnection);

        services.AddDbContext<PlatformDbContext>(options =>
            ConfigureDatabase(options, provider, platformConnection, PlatformHistory));

        services.AddIdentityCore<PlatformUser>(IdentityStoreConfiguration.Configure)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<PlatformDbContext>();
        services.Replace(ServiceDescriptor.Scoped<IUserStore<PlatformUser>, PlatformIdentityStore>());
        services.TryAddScoped<FactorStore, UnavailableFactorStore>();

        services.AddOpenIddict().AddCore(options =>
        {
            options.UseEntityFrameworkCore().UseDbContext<PlatformDbContext>();
            // Separate processes must see current administrative state; no stale entity caches.
            options.DisableEntityCaching();
        });

        return services;
    }

    /// <summary>Configures only the isolated session store and its fixed migration history.</summary>
    /// <example><code>services.AddSessionPersistence(provider, sessionConnection);</code></example>
    public static IServiceCollection AddSessionPersistence(
        this IServiceCollection services, IRelationalDatabaseProvider provider, string sessionConnection)
    {
        const string SessionHistory = "SessionMigrations";
        provider.Connections.Validate(sessionConnection);
        services.AddDbContext<AdminSessionDbContext>(options =>
            ConfigureDatabase(options, provider, sessionConnection, SessionHistory));
        return services;
    }

    /// <summary>Configures one selected relational implementation and native OpenIddict mappings.</summary>
    /// <example><code>PersistenceRegistration.ConfigureDatabase(options, provider, connection, "PlatformMigrations");</code></example>
    public static void ConfigureDatabase(DbContextOptionsBuilder options, IRelationalDatabaseProvider provider,
        string connection, string historyTable)
    {
        options.UseDatabaseProvider(provider, connection, DatabaseStores.FromHistory(historyTable));
        options.UseOpenIddict();
        // No sensitive-data logging or implicit execution-strategy retries.
    }
}
