using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Testing;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Ceremonies;

/// <summary>Uses independent accounts to ensure global and browser limits cannot be masked by per-account limits.</summary>
[Collection("administrator-bootstrap")]
public sealed class CapacityRegressionTests(BootstrapFixture fixture)
{
    private const string OtherUser = "synthetic-capacity-owner";
    private const string Enrollment = "Enrollment";
    private const string Stamp = "synthetic-stamp";
    private const int BrowserLimit = 8;
    private const int InstallationLimit = 10000;
    private const int Lifetime = 300;

    /// <summary>A browser already owning eight other-account flows cannot obtain a ninth for an otherwise empty account.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_FullBrowserBudget_When_DifferentAccountRequestsAdmission_Then_PerAccountSpaceCannotBypassBrowserLimit(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var secret = CeremonySecrets.Create();
        var budget = CeremonySecrets.Digest(secret);
        factors.Database.Add(new AdministratorEnrollment { UserId = OtherUser });
        factors.Database.Add(new AdministratorAuthentication { UserId = OtherUser });
        factors.Database.Add(new AdministratorBrowserBudget { Id = budget, ExpiresAt = now + Lifetime });
        factors.Database.AddRange(Enumerable.Range(0, BrowserLimit).Select(_ => new AdministratorInteraction
        {
            UserId = OtherUser,
            PublicId = CeremonySecrets.Create(),
            BrowserBudgetId = budget,
            BrowserDigest = Stamp,
            SecurityStamp = Stamp,
            Purpose = Enrollment,
            CreatedAt = now,
            ExpiresAt = now + Lifetime
        }));
        await factors.Database.SaveChangesAsync(factors.Token);
        await using var transaction = await CeremonyTransaction.OpenAsync(factors.Database, scenario.Get<NativeOperation>(),
            new(false, true, [budget], [scenario.UserId]), factors.Token);
        var account = await scenario.AccountAsync(transaction);
        var password = await CeremonyPassword.VerifyAsync(factors.Users, scenario.Get<CeremonyBudget>(), scenario.Get<CeremonyClock>(), transaction, account, Enrollment, lease.Password);
        Assert.NotNull(password);
        Assert.Null(await scenario.Get<CeremonyAdmission>().CreateAsync(transaction, account, secret, Enrollment, 1, scenario.Secret, password));
        Assert.Equal(0, await factors.Database.Set<AdministratorInteraction>().CountAsync(row => row.UserId == scenario.UserId, factors.Token));
    }

    /// <summary>An account with no retained sessions still cannot allocate after another account fills installation storage.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_InstallationSessionCeiling_When_EmptyAccountQueriesCapacity_Then_GlobalRetainedLimitDenies(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        factors.Database.Add(new AdministratorEnrollment { UserId = OtherUser });
        factors.Database.Add(new AdministratorAuthentication { UserId = OtherUser });
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        factors.Database.AddRange(Enumerable.Range(0, InstallationLimit).Select(_ => OtherSession(scenario, now)));
        await factors.Database.SaveChangesAsync(factors.Token);
        await using var transaction = await scenario.OpenAsync(admission: true);
        Assert.False(await scenario.Get<CeremonyAdmission>().HasSessionCapacityAsync(transaction, scenario.UserId));
        Assert.Equal(0, await factors.Database.Set<AdministratorSession>().CountAsync(row => row.UserId == scenario.UserId, factors.Token));
        Assert.Equal(10000, await factors.Database.Set<AdministratorSession>().CountAsync(factors.Token));
    }

    /// <summary>Creates deliberately high retained occupancy without generating authentication authority or cookies.</summary>
    private static AdministratorSession OtherSession(CeremonyScenario scenario, long now)
    {
        var session = AdmissionTests.Session(scenario, now, revoked: true);
        session.UserId = OtherUser;
        return session;
    }

    /// <summary>Delayed allocation retains the actual password-verification timestamp instead of inventing fresh proof.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_VerifiedPassword_When_AdmissionIsDelayed_Then_OriginalVerificationTimeIsPreserved(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        var clock = new FactorClock(DateTimeOffset.UtcNow);
        await using var services = new FactorServices(provider, lease, material, clock);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await using var transaction = await scenario.OpenAsync(admission: true);
        var account = await scenario.AccountAsync(transaction);
        var password = await CeremonyPassword.VerifyAsync(factors.Users, scenario.Get<CeremonyBudget>(), scenario.Get<CeremonyClock>(), transaction, account, Enrollment, lease.Password);
        Assert.NotNull(password);
        var verified = clock.Now.ToUnixTimeSeconds();
        const int DelaySeconds = 120;
        clock.Now = clock.Now.AddSeconds(DelaySeconds);
        var interaction = await scenario.Get<CeremonyAdmission>().CreateAsync(transaction, account, scenario.BudgetSecret, Enrollment, 1, scenario.Secret, password);
        Assert.NotNull(interaction);
        Assert.Equal(verified, interaction.PasswordVerifiedAt);
        Assert.Equal(verified + DelaySeconds, interaction.CreatedAt);
    }
}
