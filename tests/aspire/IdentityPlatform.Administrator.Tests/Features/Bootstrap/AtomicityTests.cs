using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Migrator.Features.Bootstrap.Initialize;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.Bootstrap;

public sealed partial class BootstrapTests
{
    private static readonly string[] Races = ["identical", "conflicting"];
    private static readonly string[] Failures = ["audit", "manager", "cancel-lock", "kill-precommit", "cancel-manager"];
    public static IEnumerable<TheoryDataRow<string, string>> RaceCases => Cases(Races);
    public static IEnumerable<TheoryDataRow<string, string>> FailureCases => Cases(Failures);

    [Theory]
    [MemberData(nameof(RaceCases))]
    public async Task Independent_processes_contend_and_only_one_initializes(string provider, string scenario)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        var secondOperation = scenario == "identical" ? lease.OperationId : Guid.NewGuid();
        await using var first = new BootstrapProcess(provider, lease.RuntimeConnection);
        await using var second = new BootstrapProcess(provider, lease.RuntimeConnection);
        await using (var held = await GovernanceLock.AcquireAsync(lease))
        {
            await first.SendAsync(lease.Input());
            await second.SendAsync(lease.Input(operation: secondOperation));
            await held.ObserveWaitersAsync(lease, 2);
        }
        var results = new[] { await first.CompleteAsync(), await second.CompleteAsync() };
        Assert.True(results.All(result => !result.Output.Contains(lease.Password, StringComparison.Ordinal)), "Bootstrap output disclosed a credential.");
        Assert.Equal(1, results.Count(result => result.ExitCode == 0 && result.Output.Contains("enrollment initialized", StringComparison.Ordinal)));
        if (scenario == "identical")
        {
            Assert.True(results.All(result => result.ExitCode == 0), "Identical operation did not reconcile.");
            Assert.Equal(1, results.Count(result => result.Output.Contains("already initialized", StringComparison.Ordinal)));
        }
        else
        {
            Assert.Equal(1, results.Count(result => result.ExitCode == 1));
        }
        var winner = results[0].Output.Contains("enrollment initialized", StringComparison.Ordinal) ? lease.OperationId : secondOperation;
        await AssertCreatedAsync(lease, lease.Password, winner);
        var before = await SnapshotAsync(lease);
        var replay = await RunAsync(lease, provider, lease.Input(operation: winner));
        Assert.True(replay.ExitCode == 0 && replay.Output.Contains("already initialized", StringComparison.Ordinal), "Winner reconciliation failed.");
        Assert.True(before == await SnapshotAsync(lease), "Winner reconciliation changed persisted state.");
    }

    [Theory]
    [MemberData(nameof(FailureCases))]
    public async Task Failed_or_interrupted_initialization_rolls_back_every_write(string provider, string scenario)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        var before = await SnapshotAsync(lease);
        switch (scenario)
        {
            case "audit":
                await lease.AuditPermissionAsync(false, Token);
                try
                {
                    var failure = await Record.ExceptionAsync(() => lease.InitializeAsync(Token));
                    Assert.True(failure is DbUpdateException && (failure.InnerException is Npgsql.PostgresException { SqlState: "42501" } ||
                        failure.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 229 }), "Expected audit INSERT permission denial.");
                }
                finally
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await lease.AuditPermissionAsync(true, cleanup.Token);
                }
                break;
            case "manager":
                await using (var rejected = lease.WithRejectedUsers())
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => lease.InitializeAsync(Token, rejected));
                }
                break;
            case "cancel-lock":
                using (var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token))
                await using (var held = await GovernanceLock.AcquireAsync(lease))
                {
                    var attempt = lease.InitializeAsync(cancelled.Token);
                    try
                    {
                        await held.ObserveWaitersAsync(lease, 1);
                        Assert.False(attempt.IsCompleted, "Operation was not blocked at the held governance row.");
                    }
                    finally
                    {
                        await CancelAndDrainAsync(attempt, cancelled);
                    }
                }
                break;
            case "cancel-manager":
                var gate = new ManagerCancellationGate();
                await using (var services = lease.WithInterceptor(gate))
                using (var cancelled = CancellationTokenSource.CreateLinkedTokenSource(Token))
                {
                    var attempt = lease.InitializeAsync(cancelled.Token, services);
                    try
                    {
                        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20), Token);
                        Assert.False(attempt.IsCompleted, "Native manager INSERT did not remain pending.");
                    }
                    finally
                    {
                        await CancelAndDrainAsync(attempt, cancelled);
                    }
                }
                break;
            case "kill-precommit":
                await using (var process = new BootstrapProcess(provider, lease.RuntimeConnection))
                await using (var held = await GovernanceLock.AcquireAsync(lease, audit: true))
                {
                    await process.SendAsync(lease.Input());
                    await held.ObserveWaitersAsync(lease, 1);
                    process.Terminate();
                    var result = await process.CompleteAsync();
                    Assert.NotEqual(0, result.ExitCode);
                }
                break;
        }
        await lease.AssertRuntimeDrainedAsync();
        Assert.True(before == await SnapshotAsync(lease), "Failed initialization left partial state.");
        Assert.Equal(BootstrapOutcome.Created, await lease.InitializeAsync(Token));
    }
    private static async Task CancelAndDrainAsync(Task attempt, CancellationTokenSource operation)
    {
        await operation.CancelAsync();
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var failure = await Record.ExceptionAsync(() => attempt.WaitAsync(cleanup.Token));
        Assert.True(attempt.IsCompleted && !cleanup.IsCancellationRequested && failure is OperationCanceledException,
            "Original operation did not finish with cancellation within independent cleanup deadline.");
    }

}
