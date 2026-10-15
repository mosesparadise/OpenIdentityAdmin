using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Ceremonies;

/// <summary>Exercises active-factor verification and rejects fabricated freshness or transplanted native proof capabilities.</summary>
[Collection("administrator-bootstrap")]
public sealed class ProofRegressionTests(BootstrapFixture fixture)
{
    private const string ClockMarker = "administrator-clock-fence-bypass";
    private const string Active = "Active";
    private const string Login = "Login";
    private const string WrongPurpose = "Recovery";
    private const int Lifetime = 300;

    /// <summary>Ordinary login uses the native protected active key, while subsequent disabled state rejects even a valid code.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ActiveFactor_When_AccountLaterBecomesDisabled_Then_NewNativeProofIsDenied(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        await InstallFixtureAsync(factors, scenario);
        var code = FactorOracle.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await using (var first = await scenario.OpenAsync())
        {
            var account = await scenario.AccountAsync(first);
            Assert.NotNull(await scenario.Get<CeremonyReplay>().VerifyAsync(first, account, (await scenario.ReadAsync(first, account))!, code, false));
            await first.CommitAsync(CeremonyEvent.Factor, true, scenario.UserId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }
        await factors.Database.Users.ExecuteUpdateAsync(update => update.SetProperty(row => row.IsEnabled, false), factors.Token);
        await using var second = await scenario.OpenAsync();
        var disabled = await scenario.AccountAsync(second);
        var unused = await CeremonyCodes.UnusedAsync(factors, code);
        Assert.Null(await scenario.Get<CeremonyReplay>().VerifyAsync(second, disabled, (await scenario.ReadAsync(second, disabled))!, unused, false));
        Assert.Equal(1, await factors.Database.Set<AdministratorReplay>().CountAsync(factors.Token));
    }

    /// <summary>Uses native store installation to seed active test state; this is explicitly not an enrollment journey acceptance test.</summary>
    private static async Task InstallFixtureAsync(FactorScenario factors, CeremonyScenario scenario)
    {
        await using var transaction = await factors.Database.Database.BeginTransactionAsync(factors.Token);
        var proof = await VerifiedStagedFactor.VerifyAsync(factors.Database, factors.Access, factors.Users, TimeProvider.System,
            scenario.Interaction.Id, FactorOracle.Code(DateTimeOffset.UtcNow.ToUnixTimeSeconds()), factors.Token);
        Assert.NotNull(proof);
        await Assert.IsType<AdministratorUserManager>(factors.Users).InstallAsync(proof, factors.Token);
        factors.User.IsEnabled = true;
        factors.Enrollment.State = Active;
        factors.Enrollment.FactorGeneration = 1;
        scenario.Interaction.Purpose = Login;
        scenario.Interaction.StagedSeed = null;
        scenario.Interaction.SecurityStamp = factors.User.SecurityStamp!;
        await factors.Database.SaveChangesAsync(factors.Token);
        await transaction.CommitAsync(factors.Token);
    }

    /// <summary>Password proof cannot change purpose, survive its transaction or refresh its five-minute maximum through allocation.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_VerifiedPassword_When_PurposeTransactionOrTimeChanges_Then_ProofCannotAuthorizeAdmission(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        var clock = new FactorClock(DateTimeOffset.UtcNow);
        await using var services = new FactorServices(provider, lease, material, clock);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        CeremonyPassword proof;
        await using (var first = await scenario.OpenAsync())
        {
            var account = await scenario.AccountAsync(first);
            proof = (await CeremonyPassword.VerifyAsync(factors.Users, scenario.Get<CeremonyBudget>(), scenario.Get<CeremonyClock>(), first, account, Login, lease.Password))!;
            Assert.NotNull(proof);
            Assert.Throws<FactorUnavailableException>(() => proof.RequireCurrent(first, account, WrongPurpose, clock.Now.ToUnixTimeSeconds()));
            clock.Now = clock.Now.AddSeconds(Lifetime);
            Assert.Throws<FactorUnavailableException>(() => proof.RequireCurrent(first, account, Login, clock.Now.ToUnixTimeSeconds()));
        }
        await using var second = await scenario.OpenAsync();
        var current = await scenario.AccountAsync(second);
        Assert.Throws<FactorUnavailableException>(() => proof.RequireCurrent(second, current, Login, proof.VerifiedAt));
    }

    /// <summary>The exact thirty-second skew is accepted but a persisted forward jump still fences a reopened transaction.</summary>
    [Trait("CriticalDecision", "administrator-clock-fence")]
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ForwardObservation_When_NewScopeRollsBack_Then_DurableFenceSurvivesRestart(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        var clock = new FactorClock(DateTimeOffset.UtcNow);
        await using var services = new FactorServices(provider, lease, material, clock);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        var original = clock.Now;
        const int ForwardSeconds = 1000;
        clock.Now = clock.Now.AddSeconds(ForwardSeconds);
        await using (var forward = await scenario.OpenAsync())
        {
            Assert.True(scenario.Get<CeremonyClock>().Observe((await scenario.AccountAsync(forward)).Authentication));
            await forward.CommitAsync(CeremonyEvent.Factor, false, scenario.UserId, clock.Now.ToUnixTimeSeconds());
        }
        await using var reopened = new FactorServices(provider, lease, material, clock);
        await using var fresh = new FactorScenario(reopened);
        var operation = fresh.Services.GetRequiredService<NativeOperation>();
        await using var transaction = await CeremonyTransaction.OpenAsync(fresh.Database, operation, new(false, false, [], [scenario.UserId]), factors.Token);
        var account = await CeremonyAccount.LoadAsync(fresh.Database, transaction, scenario.UserId);
        var policy = fresh.Services.GetRequiredService<CeremonyClock>();
        const int AllowedSkew = 30;
        clock.Now = clock.Now.AddSeconds(-AllowedSkew);
        Assert.True(policy.Observe(account.Authentication));
        clock.Now = original;
        Assert.True(!policy.Observe(account.Authentication), ClockMarker);
        Assert.Equal(original.ToUnixTimeSeconds() + ForwardSeconds, account.Authentication.ObservedAt);
    }
}
