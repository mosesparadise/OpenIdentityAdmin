using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Verifies finite tombstone cleanup and strict retention without disturbing live or future-observed interactions.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedCacheCleanupTests(BootstrapFixture fixture)
{
    private const int CleanupLimit = 100;
    private const int RetentionSeconds = 14 * 24 * 60 * 60;
    private const int LifetimeSeconds = 300;

    /// <summary>One cache request removes at most one hundred eligible tombstones and preserves exact-retention equality and future observations.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_MixedRetainedInteractions_When_RealCacheRunsCleanup_Then_OnlyStrictlyEligibleBoundedBatchIsRemoved(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var now = test.Http.Now.ToUnixTimeSeconds();
        var expired = Enumerable.Range(0, CleanupLimit + 1).Select(_ => Expired(test, now)).ToArray();
        var equality = Expired(test, now);
        equality.RetainUntil = now;
        var future = Expired(test, now);
        future.ObservedAt = now + 1;
        var live = ManagedCacheCapacityTests.Row(test, false);
        await using (var scope = test.Http.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            database.AddRange(expired);
            database.AddRange(equality, future, live);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var response = await test.SendAsync(ManagedCacheTestContext.Initial());
        _ = await test.ResumeAsync(response);
        var after = await test.SnapshotAsync();
        Assert.Equal(5, after.Interactions.Length);
        Assert.Contains(after.Interactions, row => row.SelectorDigest == equality.SelectorDigest);
        Assert.Contains(after.Interactions, row => row.SelectorDigest == future.SelectorDigest);
        Assert.Contains(after.Interactions, row => row.SelectorDigest == live.SelectorDigest && row.State == ManagedLoginState.PendingPassword);
        Assert.Single(after.Interactions, row => expired.Any(original => original.SelectorDigest == row.SelectorDigest));
    }

    /// <summary>Creates a terminal row whose original age and expiration are strictly beyond the independent fourteen-day minimum.</summary>
    private static ManagedLoginInteraction Expired(ManagedCacheTestContext test, long now)
    {
        var row = ManagedCacheCapacityTests.Row(test, true);
        row.CreatedAt = now - RetentionSeconds - LifetimeSeconds;
        row.ExpiresAt = row.CreatedAt + LifetimeSeconds;
        row.ObservedAt = row.ExpiresAt;
        row.RetainUntil = now - 1;
        return row;
    }
}
