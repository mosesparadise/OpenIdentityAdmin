using System.Diagnostics;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Ceremonies;

/// <summary>Observes cleanup contention through real provider locks and verifies rollback and later physical reclamation.</summary>
[Collection("administrator-bootstrap")]
public sealed class CleanupConcurrencyTests(BootstrapFixture fixture)
{
    private const string AdmissionStage = "%UPDATE%AdministratorAdmission%";
    private const int CompetingCleaners = 2;
    private const string Consumed = "Consumed";

    /// <summary>A blocked cleanup batch cancels without deleting state; a later independent pass reclaims only terminal rows.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ContendedCleanup_When_PassIsCancelled_Then_BatchRollsBackAndNextPassCanReclaim(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        scenario.Interaction.State = Consumed;
        await factors.Database.SaveChangesAsync(factors.Token);
        await using var scope = services.Services.CreateAsyncScope();
        var cleaner = scope.ServiceProvider.GetRequiredService<CeremonyCleanup>();
        await using (var held = await scenario.OpenAsync(admission: true))
        {
            var blocker = await DatabaseWaiters.IdentifyAsync(factors.Database, factors.Token);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(factors.Token);
            var waiting = cleaner.RunAsync(stop.Token);
            string? diagnostic;
            try
            {
                await DatabaseWaiters.ObserveAsync(lease, blocker, AdmissionStage, 1);
            }
            finally { diagnostic = await CeremonyCancellation.CancelAndDrainAsync(waiting, stop, factors.Database.Database.IsSqlServer()); }
            Assert.True(diagnostic is null, diagnostic);
            Assert.Equal(1, await factors.Database.Set<AdministratorInteraction>().CountAsync(factors.Token));
        }
        Assert.Equal(1, await cleaner.RunAsync(factors.Token));
        Assert.Equal(0, await factors.Database.Set<AdministratorInteraction>().CountAsync(factors.Token));
        Assert.Equal(1, await factors.Database.Set<AdministratorBootstrapReceipt>().CountAsync(factors.Token));
    }

    /// <summary>The production deadline expires without caller cancellation and the retained row remains reclaimable afterward.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_BlockedCleanup_When_ProductionDeadlineExpires_Then_BoundedFailurePreservesRetainedState(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        scenario.Interaction.State = Consumed;
        await factors.Database.SaveChangesAsync(factors.Token);
        await using var scope = services.Services.CreateAsyncScope();
        var cleaner = scope.ServiceProvider.GetRequiredService<CeremonyCleanup>();
        await using (var held = await scenario.OpenAsync(admission: true))
        {
            var blocker = await DatabaseWaiters.IdentifyAsync(factors.Database, factors.Token);
            var elapsed = Stopwatch.StartNew();
            var waiting = cleaner.RunAsync(factors.Token);
            const int DrainSeconds = 15;
            const int MinimumElapsedSeconds = 3;
            try
            {
                await DatabaseWaiters.ObserveAsync(lease, blocker, AdmissionStage, 1);
                var failure = await Record.ExceptionAsync(() => waiting.WaitAsync(TimeSpan.FromSeconds(DrainSeconds), factors.Token));
                Assert.True(waiting.IsCompleted);
                Assert.IsAssignableFrom<OperationCanceledException>(failure);
                Assert.InRange(elapsed.Elapsed.TotalSeconds, MinimumElapsedSeconds, DrainSeconds);
                Assert.False(factors.Token.IsCancellationRequested);
                Assert.Equal(1, await factors.Database.Set<AdministratorInteraction>().CountAsync(factors.Token));
            }
            finally { await DrainAllAsync([waiting]); }
        }
        Assert.Equal(1, await cleaner.RunAsync(factors.Token));
        Assert.Equal(0, await factors.Database.Set<AdministratorInteraction>().CountAsync(factors.Token));
    }

    /// <summary>Concurrent cleanup passes serialize physical retained-capacity release and cannot erase a live interaction.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_TwoCleaners_When_TerminalSessionDeletionContends_Then_PhysicalRowsAreReclaimedOnce(string provider)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        using var material = new IssuerMaterialFixture();
        await using var services = new FactorServices(provider, lease, material);
        await using var factors = new FactorScenario(services);
        await factors.InitializeAsync(lease);
        var scenario = new CeremonyScenario(factors);
        await scenario.StageAsync();
        factors.Database.Add(AdmissionTests.Session(scenario, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), revoked: true));
        await factors.Database.SaveChangesAsync(factors.Token);
        await using var firstScope = services.Services.CreateAsyncScope();
        await using var secondScope = services.Services.CreateAsyncScope();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(factors.Token);
        Task<int>[] attempts;
        await using (var held = await scenario.OpenAsync(admission: true))
        {
            var blocker = await DatabaseWaiters.IdentifyAsync(factors.Database, factors.Token);
            attempts = [firstScope.ServiceProvider.GetRequiredService<CeremonyCleanup>().RunAsync(stop.Token),
                secondScope.ServiceProvider.GetRequiredService<CeremonyCleanup>().RunAsync(stop.Token)];
            try
            {
                await DatabaseWaiters.ObserveAsync(lease, blocker, AdmissionStage, CompetingCleaners);
            }
            catch { await stop.CancelAsync(); await DrainAllAsync(attempts); throw; }
        }
        try
        {
            Assert.Equal(1, (await Task.WhenAll(attempts)).Sum());
        }
        finally { await stop.CancelAsync(); await DrainAllAsync(attempts); }
        Assert.Equal(0, await factors.Database.Set<AdministratorSession>().CountAsync(factors.Token));
        Assert.Equal(1, await factors.Database.Set<AdministratorInteraction>().CountAsync(factors.Token));
    }

    /// <summary>Bounds independent cleanup of contenders without allowing a background database task to outlive its scope.</summary>
    private static async Task DrainAllAsync(IEnumerable<Task> tasks)
    {
        const int DrainSeconds = 40;
        var completion = Task.WhenAll(tasks);
        _ = await Record.ExceptionAsync(() => completion.WaitAsync(TimeSpan.FromSeconds(DrainSeconds)));
        Assert.True(completion.IsCompleted);
    }
}
