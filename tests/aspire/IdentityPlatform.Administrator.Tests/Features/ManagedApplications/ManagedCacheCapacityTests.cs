using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Exercises independent managed interaction and native artifact ceilings through actual owned HTTPS cache creation.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedCacheCapacityTests(BootstrapFixture fixture)
{
    private const int DigestLength = 64;
    private const int BrowserLimit = 8;
    private const int LiveLimit = 128;
    private const int RetainedLimit = 10000;
    private const int BatchSize = 250;
    private const int LifetimeSeconds = 300;
    private const int RetentionSeconds = 14 * 24 * 60 * 60;

    /// <summary>Eight real attached interactions fit one protected browser; the next request cannot evict or replace one.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_SameManagedBrowser_When_EightCachesExist_Then_NinthDeniesWithoutReplacingAnyNativeRequest(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        for (var count = 0; count < BrowserLimit; count++)
        {
            using var response = await test.SendAsync(ManagedCacheTestContext.Initial());
            _ = await test.ResumeAsync(response);
        }
        var before = await test.SnapshotAsync();
        Assert.Equal(BrowserLimit, before.Interactions.Length);
        Assert.Single(before.Interactions.Select(row => row.BrowserDigest).Distinct());
        using var denied = await test.SendAsync(ManagedCacheTestContext.Initial());
        await ManagedCacheTestContext.UnavailableAsync(denied);
        var after = await test.SnapshotAsync();
        Assert.Equal(before.Interactions.Select(row => row.SelectorDigest), after.Interactions.Select(row => row.SelectorDigest));
        Assert.Equal(before.Tokens.Select(row => row.Id), after.Tokens.Select(row => row.Id));
    }

    /// <summary>Independently seeded global-live and retained occupancy admit exactly the final slot with no hidden native-capacity substitute.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_LastManagedInteractionSlot_When_CacheThenOverflowArrive_Then_EqualitySucceedsAndNextDenies(string provider, bool retained)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var limit = retained ? RetainedLimit : LiveLimit;
        await FillInteractionsAsync(test, limit - 1, retained);
        using var final = await test.SendAsync(ManagedCacheTestContext.Initial());
        _ = await test.ResumeAsync(final);
        using var overflow = await test.SendAsync(ManagedCacheTestContext.Initial());
        await ManagedCacheTestContext.UnavailableAsync(overflow);
        var actual = await test.SnapshotAsync();
        Assert.Equal(limit, actual.Interactions.Length);
        Assert.Single(actual.Tokens);
    }

    /// <summary>Native tokens can exhaust their separate managed limit while interaction capacity is empty.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_IndependentNativeRequestLimit_When_InteractionCapacityIsEmpty_Then_ExtraCacheStillDenies(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        await using (var scope = test.Http.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var application = await database.Set<OpenIddictEntityFrameworkCoreApplication>().SingleAsync(row => row.Id == test.Id, TestContext.Current.CancellationToken);
            database.AddRange(Enumerable.Range(0, LiveLimit).Select(_ => new OpenIddictEntityFrameworkCoreToken
            {
                Application = application,
                Type = OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken,
                Status = OpenIddictConstants.Statuses.Valid,
                CreationDate = test.Http.Now.UtcDateTime,
                ExpirationDate = test.Http.Now.AddSeconds(LifetimeSeconds).UtcDateTime
            }));
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var response = await test.SendAsync(ManagedCacheTestContext.Initial());
        await ManagedCacheTestContext.UnavailableAsync(response);
        var actual = await test.SnapshotAsync();
        Assert.Empty(actual.Interactions);
        Assert.Equal(LiveLimit, actual.Tokens.Length);
    }

    /// <summary>Creates only disposable quota metadata in bounded fixture batches; no seeded row is accepted as human authentication.</summary>
    internal static async Task FillInteractionsAsync(ManagedCacheTestContext test, int count, bool retained)
    {
        for (var offset = 0; offset < count; offset += BatchSize)
        {
            await using var scope = test.Http.Scope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            database.AddRange(Enumerable.Range(0, Math.Min(BatchSize, count - offset)).Select(_ => Row(test, retained)));
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Builds independently specified original deadlines and retention, leaving all authentication fields absent.</summary>
    internal static ManagedLoginInteraction Row(ManagedCacheTestContext test, bool retained)
    {
        var now = test.Http.Now.ToUnixTimeSeconds();
        return new()
        {
            SelectorDigest = NativeCacheProfile.Random().PadRight(DigestLength, 'A'),
            BrowserDigest = NativeCacheProfile.Random().PadRight(DigestLength, 'B'),
            DemandDigest = NativeCacheProfile.Random().PadRight(DigestLength, 'C'),
            ApplicationId = test.Id,
            ApplicationRevision = 1,
            Revision = 1,
            State = retained ? ManagedLoginState.Denied : ManagedLoginState.PendingPassword,
            CreatedAt = now,
            ObservedAt = now,
            ExpiresAt = now + LifetimeSeconds,
            RetainUntil = now + RetentionSeconds,
            CorrelationId = Guid.NewGuid()
        };
    }
}
