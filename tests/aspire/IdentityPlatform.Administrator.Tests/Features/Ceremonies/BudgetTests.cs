using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Testing;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Ceremonies;

/// <summary>Proves durable shared attempt limits and exact native lockout boundaries on both runtime providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class BudgetTests(BootstrapFixture fixture)
{
    private const string AuditMarker = "administrator-audit-bypass";
    private const string PasswordBudgetMarker = "administrator-password-budget-bypass";
    private const int Failures = 5;
    private const int LockoutSeconds = 300;
    private const string Consumed = "Consumed";

    /// <summary>Correct passwords between factor failures never reset the budget; the fifth failure locks and consumes atomically.</summary>
    [Trait("CriticalDecision", "administrator-password-budget")]
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_FactorFailures_When_CorrectPasswordsIntervene_Then_FifthFailureLocksAcrossScopes(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        var clock = new FactorClock(DateTimeOffset.UtcNow);
        await using var services = new FactorServices(provider, lease, material, clock);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        clock.Now = DateTimeOffset.UtcNow;
        for (var attempt = 1; attempt <= Failures; attempt++)
        {
            await using var transaction = await scenario.OpenAsync();
            var account = await scenario.AccountAsync(transaction);
            Assert.True(await scenario.Get<CeremonyBudget>().OpenAsync(transaction, account));
            Assert.NotNull(await CeremonyPassword.VerifyAsync(factors.Users, scenario.Get<CeremonyBudget>(), scenario.Get<CeremonyClock>(),
                transaction, account, scenario.Interaction.Purpose, lease.Password));
            Assert.True(account.Authentication.Failures == attempt - 1, PasswordBudgetMarker);
            var interaction = await scenario.ReadAsync(transaction, account);
            Assert.NotNull(interaction);
            await scenario.Get<CeremonyBudget>().FailAsync(transaction, account, interaction);
            await transaction.CommitAsync(CeremonyEvent.Factor, false, scenario.UserId, clock.Now.ToUnixTimeSeconds());
        }
        await using var final = await scenario.OpenAsync();
        var locked = await scenario.AccountAsync(final);
        Assert.False(await scenario.Get<CeremonyBudget>().OpenAsync(final, locked));
        Assert.Equal(5, locked.Authentication.Failures);
        Assert.Equal(clock.Now.ToUnixTimeSeconds() + LockoutSeconds, locked.User.LockoutEnd!.Value.ToUnixTimeSeconds());
        Assert.Equal(Consumed, (await factors.Database.Set<AdministratorInteraction>().SingleAsync(scenario.Token)).State);
        Assert.Equal(6, await factors.Database.Set<SecurityAuditIntent>().CountAsync(scenario.Token));
    }

    /// <summary>Only exact lockout expiry reopens a budget; expired interactions remain unusable and no session is created.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ExpiredLockout_When_FirstSerializedAttemptArrives_Then_OnlyBudgetReopens(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        var clock = new FactorClock(DateTimeOffset.UtcNow);
        await using var services = new FactorServices(provider, lease, material, clock);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await SeedLockoutAsync(factors, clock.Now.ToUnixTimeSeconds());
        await using (var before = await scenario.OpenAsync())
        {
            Assert.False(await scenario.Get<CeremonyBudget>().OpenAsync(before, await scenario.AccountAsync(before)));
        }
        clock.Now = clock.Now.AddSeconds(LockoutSeconds);
        await using var exact = await scenario.OpenAsync();
        var account = await scenario.AccountAsync(exact);
        Assert.True(await scenario.Get<CeremonyBudget>().OpenAsync(exact, account));
        Assert.Equal(0, account.Authentication.Failures);
        Assert.Null(account.Authentication.LockoutUntil);
        Assert.Null(account.User.LockoutEnd);
        Assert.Equal(0, account.User.AccessFailedCount);
        await exact.CommitAsync(CeremonyEvent.Password, false, scenario.UserId, clock.Now.ToUnixTimeSeconds());
        Assert.Empty(await factors.Database.Set<AdministratorSession>().ToArrayAsync(scenario.Token));
    }

    /// <summary>Seeds existing consistent lockout metadata independently of the implementation's policy constants.</summary>
    private static async Task SeedLockoutAsync(FactorScenario factors, long now)
    {
        var authentication = await factors.Database.Set<AdministratorAuthentication>().SingleAsync(factors.Token);
        authentication.Failures = Failures;
        authentication.LockoutUntil = now + LockoutSeconds;
        factors.User.LockoutEnd = DateTimeOffset.FromUnixTimeSeconds(authentication.LockoutUntil.Value);
        factors.User.AccessFailedCount = Failures;
        await factors.Database.SaveChangesAsync(factors.Token);
    }

    /// <summary>Native future lockout and disabled native lockout eligibility both deny without silently repairing state.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_IneligibleNativeState_When_BudgetOpens_Then_NoAutomaticRepairOccurs(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        factors.User.LockoutEnd = DateTimeOffset.UtcNow.AddSeconds(LockoutSeconds);
        await factors.Database.SaveChangesAsync(factors.Token);
        await using (var first = await scenario.OpenAsync())
        {
            Assert.False(await scenario.Get<CeremonyBudget>().OpenAsync(first, await scenario.AccountAsync(first)));
        }
        await factors.Database.Users.ExecuteUpdateAsync(update => update.SetProperty(row => row.LockoutEnabled, false).SetProperty(row => row.LockoutEnd, (DateTimeOffset?)null), factors.Token);
        await using var second = await scenario.OpenAsync();
        Assert.False(await scenario.Get<CeremonyBudget>().OpenAsync(second, await scenario.AccountAsync(second)));
    }

    /// <summary>Audit denial after a native lockout update rolls back the native user, product count and interaction together.</summary>
    [Trait("CriticalDecision", "administrator-audit")]
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_LastFailedAttempt_When_AuditInsertIsDenied_Then_AllNativeAndProductWritesRollBack(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        const int BeforeThreshold = 4;
        await factors.Database.Set<AdministratorAuthentication>().ExecuteUpdateAsync(update => update.SetProperty(row => row.Failures, BeforeThreshold), factors.Token);
        await lease.AuditPermissionAsync(false, factors.Token);
        try
        {
            await using var transaction = await scenario.OpenAsync();
            var account = await scenario.AccountAsync(transaction);
            await scenario.Get<CeremonyBudget>().FailAsync(transaction, account);
            var failure = await Record.ExceptionAsync(() => transaction.CommitAsync(CeremonyEvent.Password, false, scenario.UserId, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            Assert.True(failure is DbUpdateException, AuditMarker);
            const string PostgreSqlPermission = "42501";
            const int SqlServerPermission = 229;
            Assert.True(failure.InnerException is Npgsql.PostgresException { SqlState: PostgreSqlPermission } ||
                failure.InnerException is Microsoft.Data.SqlClient.SqlException { Number: SqlServerPermission });
        }
        finally { await lease.AuditPermissionAsync(true, factors.Token); }
        Assert.Equal(4, (await factors.Database.Set<AdministratorAuthentication>().SingleAsync(factors.Token)).Failures);
        Assert.Null((await factors.Database.Users.SingleAsync(factors.Token)).LockoutEnd);
        Assert.Equal(1, await factors.Database.Set<SecurityAuditIntent>().CountAsync(factors.Token));
    }
}
