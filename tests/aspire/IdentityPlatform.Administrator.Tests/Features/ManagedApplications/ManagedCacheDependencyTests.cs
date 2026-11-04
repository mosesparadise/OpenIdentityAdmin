using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Exercises cleanup against actual generated request dependencies, including a later database expiration that must prevent deletion.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedCacheDependencyTests(BootstrapFixture fixture)
{
    private const int RetentionDays = 14;
    private const int AfterRetentionSeconds = 600;

    /// <summary>A real attached native request is removed only after retention; a still-live dependency preserves both token and tombstone.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_RealAttachedCache_When_RetentionPasses_Then_OnlyExpiredNativeDependencyCanBeDeleted(string provider, bool liveDependency)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        using var first = await test.SendAsync(ManagedCacheTestContext.Initial());
        _ = await test.ResumeAsync(first);
        var original = Assert.Single((await test.SnapshotAsync()).Interactions);
        var future = test.Http.Now.AddDays(RetentionDays).AddSeconds(AfterRetentionSeconds);
        await using (var scope = test.Http.Scope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            Assert.Equal(1, await database.Set<ApplicationPolicy>().Where(row => row.ApplicationId == test.Id)
                .ExecuteUpdateAsync(setter => setter.SetProperty(row => row.CredentialExpiresAt, future.AddHours(1).ToUnixTimeSeconds()), TestContext.Current.CancellationToken));
            if (liveDependency)
            {
                Assert.Equal(1, await database.Set<OpenIddictEntityFrameworkCoreToken>().Where(row => row.Id == original.RequestId)
                    .ExecuteUpdateAsync(setter => setter.SetProperty(row => row.ExpirationDate, future.AddMinutes(1).UtcDateTime), TestContext.Current.CancellationToken));
            }
        }
        test.Http.Environment.Issuer.Account.Journey.Clock.Now = future;
        using var next = await test.SendAsync(ManagedCacheTestContext.Initial());
        _ = await test.ResumeAsync(next);
        var actual = await test.SnapshotAsync();
        Assert.Equal(liveDependency ? 2 : 1, actual.Interactions.Length);
        Assert.Equal(liveDependency ? 2 : 1, actual.Tokens.Length);
        Assert.Equal(liveDependency, actual.Tokens.Any(row => row.Id == original.RequestId));
        if (liveDependency)
        {
            Assert.Equal(ManagedLoginState.Expired, Assert.Single(actual.Interactions, row => row.SelectorDigest == original.SelectorDigest).State);
        }
    }
}
