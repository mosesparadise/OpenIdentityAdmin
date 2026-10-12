using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Ceremonies;

/// <summary>Checks live and physical retained capacity independently, using actual configured ceilings and database rows.</summary>
[Collection("administrator-bootstrap")]
public sealed class AdmissionTests(BootstrapFixture fixture)
{
    private const string Enrollment = "Enrollment";
    private const string Consumed = "Consumed";
    private const int LiveLimit = 8;
    private const int RetainedLimit = 10000;
    private const int Lifetime = 300;

    /// <summary>Eight independent browsers cannot bypass the per-account limit; consumed rows release live capacity without eviction.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_EightLiveInteractions_When_AnotherBrowserRequestsAdmission_Then_NoExistingFlowIsEvicted(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        for (var index = 0; index < LiveLimit; index++)
        {
            await using var transaction = await scenario.OpenAsync(admission: true);
            Assert.NotNull(await AdmitAsync(scenario, transaction, lease.Password));
            await transaction.CommitAsync(CeremonyEvent.Password, true, scenario.UserId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }
        await using (var denied = await scenario.OpenAsync(admission: true))
        {
            Assert.Null(await AdmitAsync(scenario, denied, lease.Password));
        }
        Assert.Equal(8, await factors.Database.Set<AdministratorInteraction>().CountAsync(factors.Token));
        Assert.Equal(8, await factors.Database.Set<AdministratorBrowserBudget>().CountAsync(factors.Token));
        await factors.Database.Set<AdministratorInteraction>().ExecuteUpdateAsync(update => update.SetProperty(row => row.State, Consumed), factors.Token);
        await using var released = await scenario.OpenAsync(admission: true);
        Assert.NotNull(await AdmitAsync(scenario, released, lease.Password));
        await released.CommitAsync(CeremonyEvent.Password, true, scenario.UserId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.Equal(9, await factors.Database.Set<AdministratorInteraction>().CountAsync(factors.Token));
    }

    /// <summary>Expired browser rows still count physically; a denied new interaction cannot allocate another browser row.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RetainedBrowserCeiling_When_NewAllocationIsRequested_Then_NoOrphanBudgetIsCreated(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        factors.Database.AddRange(Enumerable.Range(0, RetainedLimit).Select(_ => new AdministratorBrowserBudget
        {
            Id = CeremonySecrets.Digest(CeremonySecrets.Create()),
            ExpiresAt = 1
        }));
        await factors.Database.SaveChangesAsync(factors.Token);
        await using (var transaction = await scenario.OpenAsync(admission: true))
        {
            Assert.Null(await AdmitAsync(scenario, transaction, lease.Password));
            Assert.Equal(10000, await factors.Database.Set<AdministratorBrowserBudget>().CountAsync(factors.Token));
            Assert.Empty(await factors.Database.Set<AdministratorInteraction>().ToArrayAsync(factors.Token));
        }
        Assert.Equal(100, await scenario.Get<CeremonyCleanup>().RunAsync(factors.Token));
        await using var reclaimed = await scenario.OpenAsync(admission: true);
        Assert.NotNull(await AdmitAsync(scenario, reclaimed, lease.Password));
        Assert.Equal(9901, await factors.Database.Set<AdministratorBrowserBudget>().CountAsync(factors.Token));
    }

    /// <summary>Consumed interactions remain at the installation ceiling until deletion even when no live interaction remains.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_RetainedInteractionCeiling_When_AllRowsAreConsumed_Then_ExpiryDoesNotReleasePhysicalCapacity(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        var template = scenario.Interaction;
        template.State = Consumed;
        factors.Database.AddRange(Enumerable.Range(1, RetainedLimit - 1).Select(_ => new AdministratorInteraction
        {
            PublicId = CeremonySecrets.Create(),
            UserId = scenario.UserId,
            BrowserBudgetId = template.BrowserBudgetId,
            BrowserDigest = template.BrowserDigest,
            Purpose = Enrollment,
            SecurityStamp = template.SecurityStamp,
            State = Consumed,
            CreatedAt = 1,
            ExpiresAt = Lifetime
        }));
        await factors.Database.SaveChangesAsync(factors.Token);
        await using var transaction = await scenario.OpenAsync(admission: true);
        Assert.Null(await AdmitAsync(scenario, transaction, lease.Password));
        Assert.Equal(10000, await factors.Database.Set<AdministratorInteraction>().CountAsync(factors.Token));
        Assert.Equal(1, await factors.Database.Set<AdministratorBrowserBudget>().CountAsync(factors.Token));
    }

    /// <summary>Separates live and retained session predicates without claiming the later HTTP completion ordering.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_SessionCapacityLimits_When_CapacityIsQueried_Then_PredicateDeniesWithoutMutatingProof(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        factors.Database.AddRange(Enumerable.Range(0, LiveLimit).Select(_ => Session(scenario, now)));
        await factors.Database.SaveChangesAsync(factors.Token);
        await using (var live = await scenario.OpenAsync(admission: true))
        {
            Assert.False(await scenario.Get<CeremonyAdmission>().HasSessionCapacityAsync(live, scenario.UserId));
        }
        await factors.Database.Set<AdministratorSession>().ExecuteUpdateAsync(update => update.SetProperty(row => row.Revoked, true), factors.Token);
        const int RemainingRetained = 24;
        factors.Database.AddRange(Enumerable.Range(0, RemainingRetained).Select(_ => Session(scenario, now, revoked: true)));
        await factors.Database.SaveChangesAsync(factors.Token);
        await using var retained = await scenario.OpenAsync(admission: true);
        Assert.False(await scenario.Get<CeremonyAdmission>().HasSessionCapacityAsync(retained, scenario.UserId));
        Assert.Equal(32, await factors.Database.Set<AdministratorSession>().CountAsync(factors.Token));
        Assert.Empty(await factors.Database.Set<AdministratorReplay>().ToArrayAsync(factors.Token));
        Assert.Equal(scenario.Interaction.ExpiresAt, (await factors.Database.Set<AdministratorInteraction>().SingleAsync(factors.Token)).ExpiresAt);
    }

    /// <summary>Builds schema-valid inert session metadata solely for retained-capacity tests, never an authenticated browser ticket.</summary>
    internal static AdministratorSession Session(CeremonyScenario scenario, long now, bool revoked = false)
    {
        const string Methods = "pwd otp";
        return new AdministratorSession
        {
            PublicId = CeremonySecrets.Create(),
            UserId = scenario.UserId,
            SecurityStamp = scenario.Interaction.SecurityStamp,
            AuthenticationMethods = Methods,
            CreatedAt = now,
            LastActivityAt = now,
            ExpiresAt = now + Lifetime,
            PasswordVerifiedAt = now,
            FactorVerifiedAt = now,
            Revoked = revoked
        };
    }

    /// <summary>Exercises production count-and-allocation with a newly generated browser and correlation proof.</summary>
    private static async Task<AdministratorInteraction?> AdmitAsync(CeremonyScenario scenario, CeremonyTransaction transaction, string password)
    {
        var account = await scenario.AccountAsync(transaction);
        var proof = await CeremonyPassword.VerifyAsync(scenario.Get<UserManager<PlatformUser>>(), scenario.Get<CeremonyBudget>(),
            scenario.Get<CeremonyClock>(), transaction, account, Enrollment, password);
        Assert.NotNull(proof);
        return await scenario.Get<CeremonyAdmission>().CreateAsync(transaction, account,
            CeremonySecrets.Create(), Enrollment, 1, CeremonySecrets.Create(), proof);
    }
}
