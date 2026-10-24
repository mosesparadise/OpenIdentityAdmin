using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Persistence;

/// <summary>Owns the isolated BFF session schema, including native client state and durable tickets.</summary>
/// <example><code>services.AddSessionPersistence(provider, configuredSessionConnection);</code></example>
public sealed class AdminSessionDbContext(DbContextOptions<AdminSessionDbContext> options)
    : DbContext(ProviderOptions.RequireStore(options, DatabaseStore.Sessions))
{
    /// <summary>Preserves native client state and adds the internal protected ticket tables.</summary>
    /// <example><code>Configure the context through AddSessionPersistence; migrations run only in the Migrator.</code></example>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        const string SessionSchema = "bff";
        modelBuilder.HasDefaultSchema(SessionSchema);
        AdminSessionModel.Configure(modelBuilder, ProviderOptions.Provider(options).Model);
        AdministratorProtocolModel.ConfigureSessions(modelBuilder, ProviderOptions.Provider(options).Model);
        BrowserSessionModel.Configure(modelBuilder, ProviderOptions.Provider(options).Model);
    }
}
