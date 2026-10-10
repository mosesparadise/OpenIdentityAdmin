using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

// Test database setup only: obtain a genuinely authenticated second subject without a product test endpoint.
public sealed class TemporaryPilotOwner : IAsyncDisposable
{
    private readonly IssuerEnvironment environment;
    private readonly PropertyValues originalManifest;
    public string UserId { get; } = Guid.NewGuid().ToString();
    public string Username { get; } = "alternate-" + Guid.NewGuid().ToString("N");

    private TemporaryPilotOwner(IssuerEnvironment environment, PropertyValues originalManifest)
    {
        this.environment = environment;
        this.originalManifest = originalManifest;
    }

    public static async Task<TemporaryPilotOwner> CreateAsync(IssuerEnvironment environment)
    {
        await using var inspection = environment.Services.CreateAsyncScope();
        var database = inspection.ServiceProvider.GetRequiredService<PlatformDbContext>();
        PilotDatabaseBounds.Apply(database);
        var manifest = await database.Set<PilotManifest>().SingleAsync(TestContext.Current.CancellationToken);
        var owner = new TemporaryPilotOwner(environment, database.Entry(manifest).CurrentValues.Clone());
        try
        {
            await using var runtime = environment.RuntimeServices();
            await using var scope = runtime.CreateAsyncScope();
            PilotDatabaseBounds.Apply(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
            var user = new PlatformUser { Id = owner.UserId, UserName = owner.Username, LockoutEnabled = true };
            var created = await scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>().CreateAsync(user, environment.Password);
            Assert.True(created.Succeeded, "Unable to create the temporary native account.");
            manifest.UserId = owner.UserId;
            manifest.UserName = owner.Username;
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            return owner;
        }
        catch
        {
            await owner.DisposeAsync();
            throw;
        }
    }

    public async Task RestoreOwnershipAsync(CancellationToken cancellationToken)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        PilotDatabaseBounds.Apply(database);
        var manifest = await database.Set<PilotManifest>().SingleAsync(cancellationToken);
        database.Entry(manifest).CurrentValues.SetValues(originalManifest);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await RestoreOwnershipAsync(deadline.Token);
        }
        catch { environment.Invalidate(); }
        try
        {
            await using var runtime = environment.RuntimeServices();
            await using var scope = runtime.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            PilotDatabaseBounds.Apply(database);
            var user = await database.Users.SingleOrDefaultAsync(value => value.Id == UserId, deadline.Token);
            if (user is not null)
            {
                var deleted = await scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>().DeleteAsync(user);
                Assert.True(deleted.Succeeded, "Unable to remove the temporary native account.");
            }
        }
        catch { environment.Invalidate(); }
        Assert.False(environment.Invalidated, "Temporary ownership/account restoration failed.");
    }
}
