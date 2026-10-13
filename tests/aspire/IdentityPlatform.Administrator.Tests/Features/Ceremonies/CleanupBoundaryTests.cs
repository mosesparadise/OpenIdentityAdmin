using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Ceremonies;

/// <summary>Verifies actual post-delete rollback and cleanup contention with admission/completion rather than only other cleaners.</summary>
[Collection("administrator-bootstrap")]
public sealed class CleanupBoundaryTests(BootstrapFixture fixture)
{
    private const string InteractionTable = "AdministratorInteraction";
    private const string SessionTable = "AdministratorSession";
    private const string AdmissionTable = "AdministratorAdmission";
    private const string AdmissionStage = "%UPDATE%AdministratorAdmission%";
    private const string Delete = "DELETE";
    private const string Update = "UPDATE";
    private const int DrainSeconds = 20;
    private const string Consumed = "Consumed";

    /// <summary>Cancellation after physical deletion rolls back that category's rows, revisions and watermarks; a fresh pass remains bounded and succeeds.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ExecutedCleanupDelete_When_CancelledBeforeCategoryCommit_Then_RowsAndMaintenanceStateAreRestored(string provider)
    {
        var gate = new DatabaseCommandGate();
        await using var scenario = await CreateAsync(provider, gate);
        await scenario.StageAsync();
        await scenario.Database.Set<AdministratorInteraction>().ExecuteUpdateAsync(update => update.SetProperty(row => row.State, Consumed), TestContext.Current.CancellationToken);
        var before = await SnapshotAsync(scenario.Database);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        gate.Arm(InteractionTable, Delete);
        var cleanup = scenario.Get<CeremonyCleanup>().RunAsync(stop.Token);
        try
        {
            _ = await gate.WaitAsync(cleanup, TestContext.Current.CancellationToken);
            await stop.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cleanup);
            await using var observer = scenario.Lease.Administrator.CreateAsyncScope();
            Assert.Equal(before, await SnapshotAsync(observer.ServiceProvider.GetRequiredService<PlatformDbContext>()));
            Assert.Equal(1, await scenario.Get<CeremonyCleanup>().RunAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, await scenario.Database.Set<AdministratorInteraction>().CountAsync(TestContext.Current.CancellationToken));
        }
        finally { gate.Release(); await DrainAsync(cleanup); }
    }

    /// <summary>A cleaner waiting behind real admission rechecks an expired budget after that admission refreshes and references it.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_ExpiredUnreferencedBudget_When_CleanupWaitsForAdmission_Then_RefreshedBudgetAndNewInteractionSurvive(string provider)
    {
        var gate = new DatabaseCommandGate();
        await using var scenario = await CreateAsync(provider, gate);
        _ = await scenario.EnrollAsync();
        var token = TestContext.Current.CancellationToken;
        await scenario.Database.Set<AdministratorInteraction>().ExecuteDeleteAsync(token);
        await scenario.Database.Set<AdministratorBrowserBudget>().ExecuteUpdateAsync(update => update.SetProperty(row => row.ExpiresAt, 1), token);
        gate.Arm(AdmissionTable, Update);
        var admission = SecurityScenario.BeginAsync(scenario, SecurityScenario.Login);
        await using var cleaner = scenario.CreateScope();
        Task<int>? cleanup = null;
        try
        {
            var blocker = await gate.WaitAsync(admission, token);
            cleanup = cleaner.ServiceProvider.GetRequiredService<CeremonyCleanup>().RunAsync(token);
            await DatabaseWaiters.ObserveAsync(scenario.Lease, blocker, AdmissionStage, 1);
            gate.Release();
            var interaction = await admission;
            Assert.Equal(0, await cleanup);
            Assert.NotNull(await SecurityScenario.InteractionAsync(scenario, interaction));
            Assert.Equal(1, await scenario.Database.Set<AdministratorBrowserBudget>().CountAsync(token));
        }
        finally { gate.Release(); await DrainAsync(admission); if (cleanup is not null) { await DrainAsync(cleanup); } }
    }

    /// <summary>A completion blocked behind session deletion consumes its native proof only after retained capacity is physically committed.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_FullRetainedSessions_When_CompletionWaitsForCleanupCommit_Then_OnlyOneNewSessionCommitsAndLiveAuthoritySurvives(string provider)
    {
        var gate = new DatabaseCommandGate();
        await using var scenario = await CreateAsync(provider, gate);
        var original = await scenario.EnrollAsync();
        await SecurityCapacity.FillSessionsAsync(scenario);
        var initial = await SecurityScenario.BeginAsync(scenario, SecurityScenario.Login);
        var code = await SecurityScenario.UnusedAsync(scenario);
        gate.Arm(SessionTable, Delete);
        await using var cleaner = scenario.CreateScope();
        var cleanup = cleaner.ServiceProvider.GetRequiredService<CeremonyCleanup>().RunAsync(TestContext.Current.CancellationToken);
        Task<JourneyCompletion.Grant?>? completion = null;
        try
        {
            var blocker = await gate.WaitAsync(cleanup, TestContext.Current.CancellationToken);
            completion = scenario.Get<JourneyCompletion>().CompleteAsync(initial, scenario.Correlation, SecurityScenario.Login, code, TestContext.Current.CancellationToken);
            await DatabaseWaiters.ObserveAsync(scenario.Lease, blocker, AdmissionStage, 1);
            gate.Release();
            Assert.Equal(32, await cleanup); // One consumed enrollment interaction and 31 revoked sessions are separate category commits.
            Assert.NotNull(await completion);
            Assert.Equal(2, await scenario.Database.Set<AdministratorSession>().CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(2, await scenario.Database.Set<AdministratorReplay>().CountAsync(TestContext.Current.CancellationToken));
            Assert.NotNull(await scenario.Get<JourneySessions>().ValidateAsync(original.UserId, original.SessionId, TestContext.Current.CancellationToken));
        }
        finally { gate.Release(); await DrainAsync(cleanup); if (completion is not null) { await DrainAsync(completion); } }
    }

    /// <summary>Registers only a scoped real-command observation seam; every manager, store, clock and transaction remains production code.</summary>
    private Task<JourneyScenario> CreateAsync(string provider, DatabaseCommandGate gate) => JourneyScenario.CreateAsync(fixture, provider,
        services => services.ConfigureDbContext<PlatformDbContext>(options => options.AddInterceptors(gate)));

    /// <summary>Includes complete security state plus admission/browser/receipt metadata omitted by the security-only witness.</summary>
    private static async Task<string> SnapshotAsync(PlatformDbContext database)
    {
        var token = TestContext.Current.CancellationToken;
        var evidence = new
        {
            Security = await SecuritySnapshot.ReadAsync(database),
            Admission = await database.Set<AdministratorAdmission>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Browsers = await database.Set<AdministratorBrowserBudget>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Receipt = await database.Set<AdministratorBootstrapReceipt>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token)
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(evidence)));
    }

    /// <summary>Drains owned database tasks before contexts or fixture databases can be disposed, including failure paths.</summary>
    private static async Task DrainAsync(Task task)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(DrainSeconds));
        _ = await Record.ExceptionAsync(() => task.WaitAsync(deadline.Token));
        Assert.True(task.IsCompleted);
    }
}
