using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Exercises genuine historical unbound interactions through all existing public human and native issuance consumers.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedScopeLegacyTests(BootstrapFixture fixture)
{
    /// <summary>A genuine cached request with the pre-migration pair can still complete fresh password, MFA, consent and cryptographically verified exchange.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_GenuineLegacyCachedRequest_When_OriginalHumanJourneyCompletes_Then_OpenIdNativeExchangeRemainsValid(string provider)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var flow = new ManagedTokenFlow(test);
        await flow.CacheAsync();
        await using (var observer = test.Http.Scope())
        {
            var database = observer.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var row = await database.Set<ManagedLoginInteraction>().SingleAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(row.PolicySnapshot);
            Assert.NotNull(row.PolicyDigest);
            row.PolicySnapshot = row.PolicyDigest = null;
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await flow.DecideAsync();
        await ManagedJourneyEvidence.ExchangeAsync(test, flow, await flow.CodeAsync(), denyManagement: true);
        var retained = Assert.Single((await test.SnapshotAsync()).Interactions);
        Assert.Null(retained.PolicySnapshot);
        Assert.Null(retained.PolicyDigest);
        Assert.Equal(ManagedLoginState.Consumed, retained.State);
    }
}
