using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Ceremonies;

/// <summary>Observes admission and eligibility changes while real requests wait for their authoritative locks.</summary>
[Collection("administrator-bootstrap")]
public sealed class StateRaceTests(BootstrapFixture fixture)
{
    private const string AdmissionStage = "%UPDATE%AdministratorAdmission%";
    private const string AccountStage = "%UPDATE%AdministratorAuthentication%";
    private const string Enrollment = "Enrollment";
    private const int DrainSeconds = 40;

    /// <summary>A waiting operation reloads native eligibility after the lock owner changes it and commits.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_WaitingAccountOperation_When_NativeEligibilityChanges_Then_PostLockReadDenies(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await using var first = await scenario.OpenAsync();
        var account = await scenario.AccountAsync(first);
        var blocker = await DatabaseWaiters.IdentifyAsync(factors.Database, factors.Token);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(factors.Token);
        var waiting = ReadBudgetAsync(services, scenario.UserId, stop.Token);
        try
        {
            await DatabaseWaiters.ObserveAsync(lease, blocker, AccountStage, 1);
            account.User.LockoutEnabled = false;
            await first.CommitAsync(CeremonyEvent.Password, false, scenario.UserId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Assert.False(await waiting);
        }
        finally { await stop.CancelAsync(); await DrainAsync(waiting); }
    }

    /// <summary>Queries actual scoped managers only after the second connection obtains the account UPDATE lock.</summary>
    private static async Task<bool> ReadBudgetAsync(FactorServices services, string userId, CancellationToken token)
    {
        await using var scope = services.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await using var transaction = await CeremonyTransaction.OpenAsync(database, scope.ServiceProvider.GetRequiredService<NativeOperation>(), new(false, false, [], [userId]), token);
        var account = await CeremonyAccount.LoadAsync(database, transaction, userId);
        return await scope.ServiceProvider.GetRequiredService<CeremonyBudget>().OpenAsync(transaction, account);
    }

    /// <summary>Competing eighth/ninth admissions serialize actual retained counts and commit exactly one new interaction.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_SevenLiveInteractions_When_TwoAdmissionsContend_Then_ExactlyOneAdditionalFlowCommits(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        await SeedSixMoreAsync(factors, scenario);
        await using var first = await scenario.OpenAsync(admission: true);
        var account = await scenario.AccountAsync(first);
        var proof = await CeremonyPassword.VerifyAsync(factors.Users, scenario.Get<CeremonyBudget>(), scenario.Get<CeremonyClock>(), first, account, Enrollment, lease.Password);
        Assert.NotNull(proof);
        Assert.NotNull(await scenario.Get<CeremonyAdmission>().CreateAsync(first, account, CeremonySecrets.Create(), Enrollment, 1, scenario.Secret, proof));
        var blocker = await DatabaseWaiters.IdentifyAsync(factors.Database, factors.Token);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(factors.Token);
        var waiting = AdmitAsync(services, scenario.UserId, lease.Password, stop.Token);
        try
        {
            await DatabaseWaiters.ObserveAsync(lease, blocker, AdmissionStage, 1);
            await first.CommitAsync(CeremonyEvent.Password, true, scenario.UserId, proof.VerifiedAt);
            Assert.False(await waiting);
        }
        finally { await stop.CancelAsync(); await DrainAsync(waiting); }
        Assert.Equal(8, await factors.Database.Set<AdministratorInteraction>().CountAsync(factors.Token));
        Assert.Equal(2, await factors.Database.Set<AdministratorBrowserBudget>().CountAsync(factors.Token));
    }

    /// <summary>Seeds independent synthetic live occupancy; no password hashes or token authority are manufactured.</summary>
    private static async Task SeedSixMoreAsync(FactorScenario factors, CeremonyScenario scenario)
    {
        const int Additional = 6;
        var template = scenario.Interaction;
        factors.Database.AddRange(Enumerable.Range(0, Additional).Select(_ => new AdministratorInteraction
        {
            UserId = template.UserId,
            PublicId = CeremonySecrets.Create(),
            BrowserBudgetId = template.BrowserBudgetId,
            BrowserDigest = template.BrowserDigest,
            SecurityStamp = template.SecurityStamp,
            Purpose = template.Purpose,
            CreatedAt = template.CreatedAt,
            ExpiresAt = template.ExpiresAt
        }));
        await factors.Database.SaveChangesAsync(factors.Token);
    }

    /// <summary>Runs an independent production password/admission transaction after the competing owner releases its locks.</summary>
    private static async Task<bool> AdmitAsync(FactorServices services, string userId, string password, CancellationToken token)
    {
        await using var scope = services.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await using var transaction = await CeremonyTransaction.OpenAsync(database, scope.ServiceProvider.GetRequiredService<NativeOperation>(), new(false, true, [], [userId]), token);
        var account = await CeremonyAccount.LoadAsync(database, transaction, userId);
        var proof = await CeremonyPassword.VerifyAsync(scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>(),
            scope.ServiceProvider.GetRequiredService<CeremonyBudget>(), scope.ServiceProvider.GetRequiredService<CeremonyClock>(), transaction, account, Enrollment, password);
        Assert.NotNull(proof);
        var interaction = await scope.ServiceProvider.GetRequiredService<CeremonyAdmission>().CreateAsync(transaction, account,
            CeremonySecrets.Create(), Enrollment, 1, CeremonySecrets.Create(), proof);
        await transaction.CommitAsync(CeremonyEvent.Password, interaction is not null, userId, proof.VerifiedAt);
        return interaction is not null;
    }

    /// <summary>Drains every contender under an independent deadline before its scope or database can be destroyed.</summary>
    private static async Task DrainAsync(Task task)
    {
        _ = await Record.ExceptionAsync(() => task.WaitAsync(TimeSpan.FromSeconds(DrainSeconds)));
        Assert.True(task.IsCompleted);
    }
}
