using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Ceremonies;

/// <summary>Checks native accepted-code protection, replay, clock fencing and atomic reservation on real providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class ReplayTests(BootstrapFixture fixture)
{
    private const string ReplayMarker = "administrator-replay-bypass";
    private const int RetentionSeconds = 300;
    private const int SkewSeconds = 30;
    private const string Corrupt = "corrupted";

    /// <summary>Native proof reserves protected evidence once; a reopened transaction rejects replay without a second reservation.</summary>
    [Trait("CriticalDecision", "administrator-replay")]
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_NativeValidCode_When_ProofIsRepeated_Then_OnlyOneProtectedReservationCommits(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        var code = FactorOracle.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await using (var first = await scenario.OpenAsync())
        {
            var account = await scenario.AccountAsync(first);
            var proof = await scenario.Get<CeremonyReplay>().VerifyAsync(first, account, (await scenario.ReadAsync(first, account))!, code, true);
            Assert.NotNull(proof);
            Assert.Equal(1, proof.Generation);
            await scenario.Get<CeremonyBudget>().CompleteAsync(first, account, proof);
            await first.CommitAsync(CeremonyEvent.Factor, true, scenario.UserId, proof.VerifiedAt);
        }
        await using var second = await scenario.OpenAsync();
        var current = await scenario.AccountAsync(second);
        Assert.True(await scenario.Get<CeremonyReplay>().VerifyAsync(second, current, (await scenario.ReadAsync(second, current))!, code, true) is null, ReplayMarker);
        var row = await factors.Database.Set<AdministratorReplay>().SingleAsync(factors.Token);
        Assert.False(row.ProtectedCode == code);
        Assert.True(factors.Protection.Unprotect(row.ProtectedCode, scenario.UserId, 1, FactorProtection.Replay) == code);
        Assert.Equal(RetentionSeconds, row.ExpiresAt - row.AcceptedAt);
        Assert.Empty(await factors.Database.Set<AdministratorSession>().ToArrayAsync(factors.Token));
    }

    /// <summary>Every retained ledger entry must decrypt; corrupt evidence cannot be ignored to admit a valid native code.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CorruptReplayEvidence_When_NativeCodeIsSubmitted_Then_ProofFailsClosed(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        factors.Database.Add(new AdministratorReplay { UserId = scenario.UserId, ProtectedCode = Corrupt, AcceptedAt = now, ExpiresAt = now + RetentionSeconds });
        await factors.Database.SaveChangesAsync(factors.Token);
        await using var transaction = await scenario.OpenAsync();
        var account = await scenario.AccountAsync(transaction);
        var interaction = (await scenario.ReadAsync(transaction, account))!;
        var code = FactorOracle.Code(now);
        await Assert.ThrowsAsync<FactorUnavailableException>(() => scenario.Get<CeremonyReplay>().VerifyAsync(transaction, account, interaction, code, true));
        Assert.Equal(1, await factors.Database.Set<AdministratorReplay>().CountAsync(factors.Token));
    }

    /// <summary>Retention is inclusive at five minutes and rollback beyond thirty seconds blocks pruning after a reopened scope.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_DurableWatermark_When_ClockRollsBack_Then_ExpiredEvidenceRemainsFenced(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        var clock = new FactorClock(DateTimeOffset.UtcNow);
        await using var services = new FactorServices(provider, lease, material, clock);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        var now = clock.Now.ToUnixTimeSeconds();
        factors.Database.Add(new AdministratorReplay { UserId = scenario.UserId, ProtectedCode = Corrupt, AcceptedAt = now - RetentionSeconds, ExpiresAt = now });
        await factors.Database.SaveChangesAsync(factors.Token);
        await using (var exact = await scenario.OpenAsync())
        {
            var account = await scenario.AccountAsync(exact);
            Assert.Equal(0, await scenario.Get<CeremonyReplay>().PruneAsync(exact, account));
            await exact.CommitAsync(CeremonyEvent.Factor, false, scenario.UserId, now);
        }
        clock.Now = clock.Now.AddSeconds(-SkewSeconds - 1);
        await using (var rollback = await scenario.OpenAsync())
        {
            var account = await scenario.AccountAsync(rollback);
            Assert.False(scenario.Get<CeremonyClock>().Observe(account.Authentication));
            Assert.Equal(0, await scenario.Get<CeremonyReplay>().PruneAsync(rollback, account));
            Assert.Equal(now, account.Authentication.ObservedAt);
        }
        clock.Now = DateTimeOffset.FromUnixTimeSeconds(now + 1);
        await using var healthy = await scenario.OpenAsync();
        Assert.Equal(1, await scenario.Get<CeremonyReplay>().PruneAsync(healthy, await scenario.AccountAsync(healthy)));
        await healthy.CommitAsync(CeremonyEvent.Factor, false, scenario.UserId, now + 1);
    }
}
