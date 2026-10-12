using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Infrastructure.Persistence;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options)
    : IdentityDbContext<PlatformUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("platform");
        LoginModelConfiguration.Configure(builder, Database.IsSqlServer());
        AdministratorModelConfiguration.Configure(builder, Database.IsSqlServer());

        if (Database.IsSqlServer())
        {
            builder.Entity<OpenIddictEntityFrameworkCoreApplication>()
                .Property(application => application.ClientId).UseCollation("Latin1_General_100_BIN2");
            builder.Entity<OpenIddictEntityFrameworkCoreScope>()
                .Property(scope => scope.Name).UseCollation("Latin1_General_100_BIN2");
        }
    }
}
