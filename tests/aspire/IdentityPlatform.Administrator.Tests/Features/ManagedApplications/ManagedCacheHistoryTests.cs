using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Proves retained native request/token and authorization limits independently of live interaction occupancy on both databases.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedCacheHistoryTests(BootstrapFixture fixture)
{
    private const int RequestLimit = 10000;
    private const int TokenLimit = 20000;
    private const int AuthorizationLimit = 2000;
    private const int BatchSize = 250;
    private const int LifetimeSeconds = 300;

    /// <summary>Exactly one final native retained slot fits; another complete native request cannot bypass either historical ceiling.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_LastRetainedNativeSlot_When_CacheThenOverflowRun_Then_ExactHistoricalTokenAndRequestLimitsHold(string provider, bool requests)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var limit = requests ? RequestLimit : TokenLimit;
        await FillAsync(test, limit - 1, requests ? OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken : OpenIddictConstants.TokenTypeIdentifiers.AccessToken);
        using var last = await test.SendAsync(ManagedCacheTestContext.Initial());
        _ = await test.ResumeAsync(last);
        using var excess = await test.SendAsync(ManagedCacheTestContext.Initial());
        await ManagedCacheTestContext.UnavailableAsync(excess);
        var after = await test.SnapshotAsync();
        Assert.Equal(limit, after.Tokens.Length);
        Assert.Single(after.Interactions);
        Assert.Equal(0, after.Authorizations);
    }

    /// <summary>Existing managed authorizations at the limit allow request-only creation; an extra authorization denies the next cache.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_ManagedAuthorizationHistory_When_CountExceedsIndependentLimit_Then_AnonymousCacheCannotAddNativeState(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        await FillAsync(test, AuthorizationLimit, null);
        using var equality = await test.SendAsync(ManagedCacheTestContext.Initial());
        _ = await test.ResumeAsync(equality);
        await FillAsync(test, 1, null);
        using var excess = await test.SendAsync(ManagedCacheTestContext.Initial());
        await ManagedCacheTestContext.UnavailableAsync(excess);
        var after = await test.SnapshotAsync();
        Assert.Equal(AuthorizationLimit + 1, after.Authorizations);
        Assert.Single(after.Tokens);
        Assert.Single(after.Interactions);
    }

    /// <summary>Seeds disposable native occupancy in bounded fixture batches; these rows carry no usable credential or authenticated subject.</summary>
    private static async Task FillAsync(ManagedCacheTestContext test, int count, string? type)
    {
        for (var offset = 0; offset < count; offset += BatchSize)
        {
            await using var scope = test.Http.Scope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var application = await database.Set<OpenIddictEntityFrameworkCoreApplication>().SingleAsync(row => row.Id == test.Id, TestContext.Current.CancellationToken);
            for (var index = 0; index < Math.Min(BatchSize, count - offset); index++)
            {
                if (type is null)
                {
                    database.Add(new OpenIddictEntityFrameworkCoreAuthorization
                    {
                        Application = application,
                        Type = OpenIddictConstants.AuthorizationTypes.AdHoc,
                        Status = OpenIddictConstants.Statuses.Revoked,
                        CreationDate = test.Http.Now.UtcDateTime
                    });
                }
                else
                {
                    database.Add(new OpenIddictEntityFrameworkCoreToken
                    {
                        Application = application,
                        Type = type,
                        Status = OpenIddictConstants.Statuses.Redeemed,
                        CreationDate = test.Http.Now.UtcDateTime,
                        ExpirationDate = test.Http.Now.AddSeconds(LifetimeSeconds).UtcDateTime
                    });
                }
            }
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }
}
