using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Coordinates a full production reader and independent governed writer using actual provider wait-graph evidence.</summary>
internal static class GovernanceReadContention
{
    private const string GovernanceStage = "%UPDATE%AdministratorGovernance%";
    private const int RequiredWriter = 1;
    private const int DrainSeconds = 10;
    private const string MissingWait = "The reader or writer ended before native singleton contention was observed.";
    private const string IncompleteCleanup = "Owned governance operations did not finish during bounded cleanup.";

    /// <summary>Requires an observed blocked writer before allowing the coherent reader to return and the real command to commit.</summary>
    internal static async Task<GovernanceObservation> RunAsync(GovernanceScenario scenario, GovernanceReadGate gate, string boundary,
        GovernanceRequest request, JourneySessionCandidate writer, string password, string code)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var reading = scenario.CreateScope();
        await using var writing = scenario.CreateScope();
        gate.Arm(reading.ServiceProvider.GetRequiredService<PlatformDbContext>(), scenario.ActorId, request.RoleId!, boundary);
        Task<GovernanceObservation>? readerTask = null;
        Task<GovernanceOutcome>? writerTask = null;
        Task? observation = null;
        try
        {
            readerTask = reading.ServiceProvider.GetRequiredService<GovernanceService>().ReadAsync(scenario.ActorId, lifetime.Token);
            var blocker = await gate.WaitAsync(readerTask, lifetime.Token);
            writerTask = writing.ServiceProvider.GetRequiredService<GovernanceService>()
                .ExecuteAsync(request, writer, password, code, lifetime.Token);
            observation = DatabaseWaiters.ObserveOwnedAsync(scenario.Journey.Lease, blocker, GovernanceStage, RequiredWriter, lifetime.Token);
            Assert.True(await Task.WhenAny(observation, readerTask, writerTask) == observation, MissingWait);
            await observation;
            Assert.False(readerTask.IsCompleted);
            Assert.False(writerTask.IsCompleted);
            gate.Release();
            var snapshot = await readerTask;
            Assert.Equal(GovernanceOutcome.Accepted, await writerTask);
            return snapshot;
        }
        finally
        {
            gate.Release();
            try
            {
                await lifetime.CancelAsync();
            }
            finally { await DrainAsync(readerTask, writerTask, observation); }
        }
    }

    /// <summary>Joins every owned operation after release and cancellation without interpreting faults as successful evidence.</summary>
    private static async Task DrainAsync(params Task?[] operations)
    {
        var completion = Task.WhenAll(operations.OfType<Task>());
        _ = await Record.ExceptionAsync(() => completion.WaitAsync(TimeSpan.FromSeconds(DrainSeconds)));
        Assert.True(completion.IsCompleted, IncompleteCleanup);
    }
}
