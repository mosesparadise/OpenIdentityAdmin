using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Ceremonies;

/// <summary>Guards security capability, replay-capacity and clock-sampling boundaries independently of product constants.</summary>
[Collection("administrator-bootstrap")]
public sealed class BoundaryTests(BootstrapFixture fixture)
{
    private const int BeforeReplayLimit = 63;
    private const int RetentionSeconds = 300;
    private const string DifferentCode = "000000";
    private const string AlternateCode = "000001";

    /// <summary>The sixty-fourth reservation succeeds and the next proof cannot evict still-live protected evidence.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_SixtyThreeReplayRows_When_TwoProofsAreAttempted_Then_LiveEvidenceIsNeverEvicted(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var code = FactorOracle.Code(now);
        var different = code == DifferentCode ? AlternateCode : DifferentCode;
        factors.Database.AddRange(Enumerable.Range(0, BeforeReplayLimit).Select(_ => new AdministratorReplay
        {
            UserId = scenario.UserId,
            AcceptedAt = now,
            ExpiresAt = now + RetentionSeconds,
            ProtectedCode = factors.Protection.Protect(different, scenario.UserId, 1, FactorProtection.Replay)
        }));
        await factors.Database.SaveChangesAsync(factors.Token);
        await using var transaction = await scenario.OpenAsync();
        var account = await scenario.AccountAsync(transaction);
        var bound = (await scenario.ReadAsync(transaction, account))!;
        Assert.NotNull(await scenario.Get<CeremonyReplay>().VerifyAsync(transaction, account, bound, code, true));
        var other = await CeremonyCodes.UnusedAsync(factors, code, bound.Row.Id, different);
        Assert.Null(await scenario.Get<CeremonyReplay>().VerifyAsync(transaction, account, bound, other, true));
        Assert.Equal(64, await factors.Database.Set<AdministratorReplay>().CountAsync(factors.Token));
    }

    /// <summary>A valid native TOTP cannot override a durable or native account lockout and reset its failures.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_LockedAccount_When_ValidNativeCodeIsPresented_Then_NoProofCanResetItsBudget(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        const int LockedFailures = 5;
        await factors.Database.Set<AdministratorAuthentication>().ExecuteUpdateAsync(update => update
            .SetProperty(row => row.Failures, LockedFailures).SetProperty(row => row.LockoutUntil, now + RetentionSeconds), factors.Token);
        await using var transaction = await scenario.OpenAsync();
        var account = await scenario.AccountAsync(transaction);
        var bound = (await scenario.ReadAsync(transaction, account))!;
        Assert.Null(await scenario.Get<CeremonyReplay>().VerifyAsync(transaction, account, bound, FactorOracle.Code(now), true));
        Assert.Equal(5, account.Authentication.Failures);
        Assert.Empty(await factors.Database.Set<AdministratorReplay>().ToArrayAsync(factors.Token));
    }

    /// <summary>A verified proof is bound to one interaction and can complete its failure-budget reset only once.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_VerifiedProof_When_CompletionRepeats_Then_SecondUseIsRejected(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        await using var transaction = await scenario.OpenAsync();
        var account = await scenario.AccountAsync(transaction);
        var bound = (await scenario.ReadAsync(transaction, account))!;
        var proof = await scenario.Get<CeremonyReplay>().VerifyAsync(transaction, account, bound, FactorOracle.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), true);
        Assert.NotNull(proof);
        await using var otherScope = services.Services.CreateAsyncScope();
        var foreignBudget = otherScope.ServiceProvider.GetRequiredService<CeremonyBudget>();
        await Assert.ThrowsAsync<FactorUnavailableException>(() => foreignBudget.CompleteAsync(transaction, account, proof));
        await scenario.Get<CeremonyBudget>().CompleteAsync(transaction, account, proof);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => scenario.Get<CeremonyBudget>().CompleteAsync(transaction, account, proof));
    }

    /// <summary>Wrong browser, changed purpose and cross-transaction account objects cannot acquire a usable proof binding.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_BoundInteraction_When_OwnerOrTransactionIsSubstituted_Then_NoAttemptIsConsumed(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        CeremonyAccount stale;
        await using (var first = await scenario.OpenAsync())
        {
            stale = await scenario.AccountAsync(first);
            Assert.Null(await CeremonyInteraction.ReadAsync(factors.Database, first, stale, scenario.Interaction.PublicId,
                CeremonySecrets.Create(), scenario.Interaction.Purpose, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        }
        await using var second = await scenario.OpenAsync();
        await Assert.ThrowsAsync<FactorUnavailableException>(() => scenario.Get<CeremonyBudget>().OpenAsync(second, stale));
        var current = await scenario.AccountAsync(second);
        var bound = (await scenario.ReadAsync(second, current))!;
        bound.Row.Purpose = DifferentCode;
        await Assert.ThrowsAsync<FactorUnavailableException>(() => scenario.Get<CeremonyBudget>().FailAsync(second, current, bound));
        Assert.Equal(0, bound.Row.Attempts);
    }

    /// <summary>Pruning uses one accepted sample; an unobserved later forward jump cannot erase evidence under an older watermark.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ChangingClock_When_ReplayIsPruned_Then_OneAcceptedSampleControlsWatermarkAndCutoff(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        var clock = new SamplingClock();
        await using var services = new FactorServices(provider, lease, material, clock);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        factors.Database.Add(new AdministratorReplay
        {
            UserId = scenario.UserId,
            ProtectedCode = DifferentCode,
            AcceptedAt = now,
            ExpiresAt = now + RetentionSeconds
        });
        await factors.Database.SaveChangesAsync(factors.Token);
        await using var transaction = await scenario.OpenAsync();
        var account = await scenario.AccountAsync(transaction);
        clock.Start(now);
        Assert.Equal(0, await scenario.Get<CeremonyReplay>().PruneAsync(transaction, account));
        Assert.Equal(1, clock.Reads);
        Assert.Equal(now, account.Authentication.ObservedAt);
    }

    /// <summary>Models a forward jump between reads without pretending to control the native authenticator clock.</summary>
    private sealed class SamplingClock : TimeProvider
    {
        private const int ForwardSeconds = 1000;
        private long? initial;
        internal int Reads
        {
            get; private set;
        }
        /// <summary>Begins a deterministic observation sequence after fixture setup.</summary>
        internal void Start(long now)
        {
            initial = now;
            Reads = 0;
        }
        /// <summary>Returns the baseline once and a later forward jump for every unexpected subsequent read.</summary>
        public override DateTimeOffset GetUtcNow() => initial is { } start
            ? DateTimeOffset.FromUnixTimeSeconds(start + (Reads++ == 0 ? 0 : ForwardSeconds)) : DateTimeOffset.UtcNow;
    }
}
