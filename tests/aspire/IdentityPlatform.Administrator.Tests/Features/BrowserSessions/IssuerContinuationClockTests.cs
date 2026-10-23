using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Uses reached provider write boundaries to prove strict original expiry and safe dependency retention.</summary>
[Collection("administrator-bootstrap")]
public sealed class IssuerContinuationClockTests(BootstrapFixture fixture)
{
    private const string ContinuationTable = "AdministratorIssuerContinuation";
    private const string Insert = "INSERT";
    private const int Lifetime = 300;
    private const int LateBinding = 299;
    private const int RetentionTolerance = 30;
    private static readonly bool[] ExpiryModes = [false, true];

    /// <summary>A clock change after the actual INSERT must either leave a clean successful context or roll back exactly at expiry.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_AnonymousInsert_When_ClockAdvancesDuringPersistence_Then_FinalCheckIsNonmutatingAndExpiryRollsBack(string provider, bool expired)
    {
        var fence = new JourneyClockFence();
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(fence)));
        var attempt = IssuerContinuationProviderFixture.Create();
        var started = scenario.Clock.Now;
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        fence.Arm(ContinuationTable, () => scenario.Clock.Now = started.AddSeconds(expired ? Lifetime : 1), Insert);
        await using var scope = scenario.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IssuerContinuationService>();
        var error = await Record.ExceptionAsync(async () => Assert.True(await service.StartAsync(attempt.Request, TestContext.Current.CancellationToken)));
        Assert.True(fence.Triggered);
        Assert.False(scope.ServiceProvider.GetRequiredService<PlatformDbContext>().ChangeTracker.HasChanges());
        if (expired)
        {
            Assert.IsType<FactorUnavailableException>(error);
            Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
        }
        else
        {
            Assert.Null(error);
            var row = await IssuerContinuationProviderFixture.RowAsync(scenario, attempt);
            Assert.Equal(started.ToUnixTimeSeconds(), row.ObservedAt);
            Assert.Equal(started.AddSeconds(Lifetime).ToUnixTimeSeconds(), row.ExpiresAt);
        }
    }

    /// <summary>A late password cannot extend interaction authority beyond continuation expiry or its cleanup retention deadline.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_LatePasswordBinding_When_OriginalRetentionPasses_Then_NoLiveInteractionOutlivesTheRemovedContinuation(string provider)
    {
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider);
        _ = await scenario.EnrollAsync();
        var attempt = IssuerContinuationProviderFixture.Create();
        Assert.True(await IssuerContinuationProviderFixture.StartAsync(scenario, attempt));
        var original = await IssuerContinuationProviderFixture.RowAsync(scenario, attempt);
        scenario.Clock.Now = scenario.Clock.Now.AddSeconds(LateBinding);
        var login = await IssuerContinuationProviderFixture.BeginAsync(scenario, attempt);
        Assert.NotNull(login.Interaction);
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var interaction = await database.Set<AdministratorInteraction>().AsNoTracking().SingleAsync(row => row.PublicId == login.Interaction, TestContext.Current.CancellationToken);
        Assert.Equal(original.ExpiresAt, interaction.ExpiresAt);
        var cleanup = scope.ServiceProvider.GetRequiredService<IssuerContinuationService>();
        scenario.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(original.ExpiresAt + RetentionTolerance);
        Assert.Equal(0, await cleanup.CleanupAsync(TestContext.Current.CancellationToken));
        scenario.Clock.Now = scenario.Clock.Now.AddSeconds(1);
        Assert.Equal(1, await cleanup.CleanupAsync(TestContext.Current.CancellationToken));
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        var code = await IssuerContinuationProviderFixture.UnusedCodeAsync(scenario);
        await IssuerContinuationProviderFixture.DenyCompletionAsync(() => IssuerContinuationProviderFixture.CompleteAsync(scenario, attempt, login.Interaction, code));
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
    }

    /// <summary>Expiry reached after the completion UPDATE rolls back the genuine native factor, new session and both audits together.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ActualMfaCompletion_When_FinalPersistenceReachesExpiry_Then_NoGrantEscapesAndOriginalProofRemainsUsable(string provider)
    {
        const string Update = "UPDATE";
        var fence = new JourneyClockFence();
        await using var scenario = await JourneyScenario.CreateAsync(fixture, provider,
            services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(fence)));
        var previous = await scenario.EnrollAsync();
        var attempt = IssuerContinuationProviderFixture.Create();
        Assert.True(await IssuerContinuationProviderFixture.StartAsync(scenario, attempt));
        var login = await IssuerContinuationProviderFixture.BeginAsync(scenario, attempt);
        Assert.NotNull(login.Interaction);
        var original = await IssuerContinuationProviderFixture.RowAsync(scenario, attempt);
        var before = await IssuerContinuationProviderFixture.SnapshotAsync(scenario);
        var code = await IssuerContinuationProviderFixture.UnusedCodeAsync(scenario);
        var validTime = scenario.Clock.Now;
        fence.Arm(ContinuationTable, () => scenario.Clock.Now = DateTimeOffset.FromUnixTimeSeconds(original.ExpiresAt), Update);
        JourneyCompletion.Grant? grant = null;
        await Assert.ThrowsAsync<FactorUnavailableException>(async () => grant =
            await IssuerContinuationProviderFixture.CompleteAsync(scenario, attempt, login.Interaction, code));
        Assert.True(fence.Triggered);
        Assert.Null(grant);
        Assert.True(before == await IssuerContinuationProviderFixture.SnapshotAsync(scenario));
        scenario.Clock.Now = validTime;
        grant = await IssuerContinuationProviderFixture.CompleteAsync(scenario, attempt, login.Interaction, code);
        Assert.NotNull(grant);
        await IssuerContinuationProviderFixture.RequireGrantAsync(scenario, attempt, login.Interaction, previous, grant);
    }

    /// <summary>Discovers advancing-success and equality-expiry outcomes for each actual provider.</summary>
    public static IEnumerable<TheoryDataRow<string, bool>> Cases =>
        from provider in IssuerContinuationProviderFixture.Providers
        from expired in ExpiryModes
        select new TheoryDataRow<string, bool>(provider, expired).WithTrait(IssuerContinuationProviderFixture.ProviderTrait, provider);
}
