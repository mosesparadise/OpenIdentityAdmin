using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Recovery;

/// <summary>Proves native optimistic-concurrency rejection rolls back an already claimed real recovery verifier.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeConcurrencyTests(BootstrapFixture fixture)
{
    /// <summary>A zero-row native user update produces genuine concurrency failure and leaves the same code usable by an explicit retry.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ClaimedRecoveryCode_When_NativeConcurrencyCheckFails_Then_EntireTransactionRollsBackBeforeExplicitRetry(string provider)
    {
        var failure = new NativeConcurrencyFailure();
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(failure)));
        var grant = await scenario.EnrollAsync();
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Recovery);
        var before = await SecuritySnapshot.ReadAsync(scenario.Database);
        failure.Armed = true;
        await Assert.ThrowsAsync<FactorUnavailableException>(() => scenario.Get<JourneyTransition>().AuthorizeAsync(initial,
            scenario.Correlation, SecurityScenario.Recovery, grant.RecoveryCodes[0], CeremonySecrets.Create(), TestContext.Current.CancellationToken));
        Assert.True(failure.ObservedClaim && failure.ObservedConcurrency);
        Assert.Equal(before, await SecuritySnapshot.ReadAsync(scenario.Database));
        Assert.NotNull(await scenario.Get<JourneyTransition>().AuthorizeAsync(initial, scenario.Correlation, SecurityScenario.Recovery,
            grant.RecoveryCodes[0], CeremonySecrets.Create(), TestContext.Current.CancellationToken));
    }
}
