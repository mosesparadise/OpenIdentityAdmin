using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Persistence;

/// <summary>Owns native identity/protocol persistence and transactionally related product state.</summary>
/// <example><code>await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);</code></example>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : IdentityDbContext<PlatformUser>(options)
{
    /// <summary>Composes native library mappings with explicit bounded feature models and ordinal protocol identifiers.</summary>
    /// <example><code>services.AddIdentityPersistence(provider, runtimeConnection); // Builds these mappings through EF.</code></example>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        const string PlatformSchema = "platform";
        const string OrdinalCollation = "Latin1_General_100_BIN2";
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(PlatformSchema);
        LoginModelConfiguration.Configure(builder, Database.IsSqlServer());
        AdministratorModelConfiguration.Configure(builder, Database.IsSqlServer());
        FactorModelConfiguration.Configure(builder, Database.IsSqlServer());

        if (Database.IsSqlServer())
        {
            builder.Entity<OpenIddictEntityFrameworkCoreApplication>()
                .Property(application => application.ClientId).UseCollation(OrdinalCollation);
            builder.Entity<OpenIddictEntityFrameworkCoreScope>()
                .Property(scope => scope.Name).UseCollation(OrdinalCollation);
        }
    }
}
