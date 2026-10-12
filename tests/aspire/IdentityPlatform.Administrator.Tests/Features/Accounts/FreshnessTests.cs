using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Exercises final policy-clock checks after actual database and native material work.</summary>
[Collection("administrator-bootstrap")]
public sealed class FreshnessTests(BootstrapFixture fixture)
{
    private const string FactorTable = "AdministratorFactor";
    private const string ReceiptTable = "AdministratorBootstrapReceipt";
    private const string RecoveryTable = "AdministratorRecoveryCode";
    private const string Insert = "INSERT";
    private const string Enrollment = "Enrollment";
    private const int IdleSeconds = 1800;
    private const int FreshSeconds = 300;
    private const int FailureLimit = 5;

    /// <summary>A native factor read crossing idle expiry cannot return session authority or advance persisted activity.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_SessionNearExpiry_When_NativeReadCrossesBoundary_Then_ValidationRollsBackActivity(string provider)
    {
        var fence = new JourneyClockFence();
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(fence)));
        var grant = await scenario.EnrollAsync();
        var original = await scenario.Database.Set<AdministratorSession>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        scenario.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(original.CreatedAt + IdleSeconds - 1);
        fence.Arm(FactorTable, () => scenario.Clock.Now = scenario.Clock.Now.AddSeconds(1));
        Assert.Null(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
        Assert.True(fence.Triggered);
        var final = await scenario.Database.Set<AdministratorSession>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(original.LastActivityAt, final.LastActivityAt);
        Assert.Equal(original.Revision, final.Revision);
    }

    /// <summary>A receipt lookup crossing interaction expiry cannot reveal the protected setup key.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_StagingNearExpiry_When_EligibilityReadCrossesBoundary_Then_SetupMaterialIsWithheld(string provider)
    {
        var fence = new JourneyClockFence();
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(fence)));
        await scenario.StageAsync();
        var interaction = await scenario.Database.Set<AdministratorInteraction>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        scenario.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(interaction.ExpiresAt - 1);
        fence.Arm(ReceiptTable, () => scenario.Clock.Now = scenario.Clock.Now.AddSeconds(1));
        Assert.Null(await scenario.Get<JourneyStaging>().ReadAsync(scenario.Interaction, scenario.Correlation, TestContext.Current.CancellationToken));
        Assert.True(fence.Triggered);
    }

    /// <summary>Native recovery generation crossing freshness rolls back the entire installation before a grant can be constructed.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_VerifiedEnrollment_When_NativeRecoveryWriteCrossesFreshness_Then_NoAuthorityCommits(string provider)
    {
        var fence = new JourneyClockFence();
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(fence)));
        await scenario.StageAsync();
        fence.Arm(RecoveryTable, () => scenario.Clock.Now = scenario.Clock.Now.AddSeconds(FreshSeconds), Insert);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => scenario.Get<JourneyCompletion>().CompleteAsync(scenario.Interaction,
            scenario.Correlation, Enrollment, FactorOracle.Code(scenario.Seed, DateTimeOffset.UtcNow.ToUnixTimeSeconds()), TestContext.Current.CancellationToken));
        Assert.True(fence.Triggered);
        Assert.Empty(await scenario.Database.Set<AdministratorSession>().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await scenario.Database.Set<AdministratorFactor>().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await scenario.Database.Set<AdministratorRecoveryCode>().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.False((await scenario.Database.Users.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).IsEnabled);
    }

    /// <summary>Malformed product budget metadata cannot reveal staging or be silently repaired by a read-only request.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_InconsistentFailureBudget_When_StagingIsRead_Then_SecretIsWithheldWithoutRepair(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        await scenario.StageAsync();
        var state = await scenario.Database.Set<AdministratorAuthentication>().SingleAsync(TestContext.Current.CancellationToken);
        state.Failures = FailureLimit;
        state.LockoutUntil = null;
        await scenario.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Null(await scenario.Get<JourneyStaging>().ReadAsync(scenario.Interaction, scenario.Correlation, TestContext.Current.CancellationToken));
        Assert.Equal(FailureLimit, (await scenario.Database.Set<AdministratorAuthentication>().SingleAsync(TestContext.Current.CancellationToken)).Failures);
    }
    /// <summary>A permitted small rollback during native work retains the highest observation without rejecting otherwise current authority.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CurrentSession_When_NativeReadObservesSmallClockRollback_Then_HighestWatermarkIsRetained(string provider)
    {
        const int AdvanceSeconds = 20;
        const int RollbackSeconds = 1;
        var fence = new JourneyClockFence();
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(fence)));
        var grant = await scenario.EnrollAsync();
        scenario.Clock.Now = scenario.Clock.Now.AddSeconds(AdvanceSeconds);
        var highest = scenario.Clock.Now.ToUnixTimeSeconds();
        fence.Arm(FactorTable, () => scenario.Clock.Now = scenario.Clock.Now.AddSeconds(-RollbackSeconds));
        Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(grant.UserId, grant.SessionId, TestContext.Current.CancellationToken));
        Assert.True(fence.Triggered);
        var state = await scenario.Database.Set<AdministratorAuthentication>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(highest, state.ObservedAt);
    }

}
