using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Health;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Migrator.Features.Database.Upgrade;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Architecture.Tests.Features;

public sealed class NativeModelTests
{
    [Theory]
    [InlineData(DatabaseProvider.PostgreSql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void Runtime_and_design_time_models_match_migrations_and_compatibility_catalog(DatabaseProvider provider)
    {
        using var designPlatform = new PlatformDesignTimeFactory().CreateDbContext([provider.ToString()]);
        using var designSessions = new SessionDesignTimeFactory().CreateDbContext([provider.ToString()]);
        Assert.False(designPlatform.Database.HasPendingModelChanges());
        Assert.False(designSessions.Database.HasPendingModelChanges());
        Assert.Equal(SchemaCatalog.Platform(provider), designPlatform.Database.GetMigrations());
        Assert.Equal(SchemaCatalog.Sessions(provider), designSessions.Database.GetMigrations());
        var services = new ServiceCollection().AddLogging().AddPlatformPersistence(provider,
            designPlatform.Database.GetConnectionString()!, designSessions.Database.GetConnectionString()!);
        using var root = services.BuildServiceProvider(validateScopes: true);
        using var scope = root.CreateScope();
        var runtimePlatform = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var runtimeSessions = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        Assert.False(runtimePlatform.Database.HasPendingModelChanges());
        Assert.False(runtimeSessions.Database.HasPendingModelChanges());
        Assert.Equal("AspNetUserPasskeys", runtimePlatform.Model.FindEntityType(typeof(IdentityUserPasskey<string>))?.GetTableName());
        Assert.DoesNotContain(runtimeSessions.Model.GetEntityTypes(), entity => entity.GetTableName()!.StartsWith("AspNet", StringComparison.Ordinal));
    }
}
