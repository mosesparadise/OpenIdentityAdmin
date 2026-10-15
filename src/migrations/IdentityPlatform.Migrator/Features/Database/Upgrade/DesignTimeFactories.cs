using IdentityPlatform.Database;
using IdentityPlatform.Migrator.Composition;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Migrator.Features.Database.Upgrade;

public sealed class PlatformDesignTimeFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>();
        DesignTimeConfiguration.Configure(options, args, "PlatformMigrations");
        return new PlatformDbContext(options.Options);
    }
}

public sealed class SessionDesignTimeFactory : IDesignTimeDbContextFactory<AdminSessionDbContext>
{
    public AdminSessionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AdminSessionDbContext>();
        DesignTimeConfiguration.Configure(options, args, "SessionMigrations");
        return new AdminSessionDbContext(options.Options);
    }
}

internal static class DesignTimeConfiguration
{
    // Only options services: shared for this design-time tool process, no connections or scopes.
    private static readonly IServiceProvider ModelServices = new ServiceCollection()
        .Configure<IdentityOptions>(IdentityStoreConfiguration.Configure).BuildServiceProvider();

    /// <summary>Uses the same explicit host registrations and model-only connection supplied by the selected provider.</summary>
    internal static void Configure(DbContextOptionsBuilder options, string[] args, string history)
    {
        var provider = DatabaseProviderRegistration.Providers.Select(args.SingleOrDefault());
        // Model generation never connects. Real migration execution uses required runtime configuration.
        var connection = provider.DesignTimeConnection;
        options.UseApplicationServiceProvider(ModelServices);
        PersistenceRegistration.ConfigureDatabase(options, provider, connection, history);
    }
}
