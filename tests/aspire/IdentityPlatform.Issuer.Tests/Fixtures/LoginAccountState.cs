using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

// Administrator-only inspection/restoration. The actual form always uses the issuer's runtime role.
public sealed class LoginAccountState : IAsyncDisposable
{
    private readonly IssuerEnvironment environment;
    private readonly string userId;
    private readonly PropertyValues original;

    private LoginAccountState(IssuerEnvironment environment, string userId, PropertyValues original)
    {
        this.environment = environment;
        this.userId = userId;
        this.original = original;
    }

    public static async Task<LoginAccountState> CaptureAsync(IssuerEnvironment environment)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var manifest = await database.Set<PilotManifest>().SingleAsync(TestContext.Current.CancellationToken);
        var user = await database.Users.SingleAsync(value => value.Id == manifest.UserId, TestContext.Current.CancellationToken);
        return new(environment, user.Id, database.Entry(user).CurrentValues.Clone());
    }

    public async ValueTask DisposeAsync()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await using var scope = environment.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var user = await database.Users.SingleAsync(value => value.Id == userId, deadline.Token);
            // Exact fixture baseline, including stamps/counters changed by real HTTP requests.
            database.Entry(user).CurrentValues.SetValues(original);
            await database.SaveChangesAsync(deadline.Token);
        }
        catch
        {
            environment.Invalidate();
        }
        Assert.False(environment.Invalidated, "The shared account baseline could not be restored.");
    }
}
