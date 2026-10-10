using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Persistence;

public sealed class AdminSessionDbContext(DbContextOptions<AdminSessionDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("bff");
    }
}
