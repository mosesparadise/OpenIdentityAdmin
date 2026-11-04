using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Migrator.Features.Bootstrap.Initialize;
using static IdentityPlatform.Administrator.Tests.Features.Bootstrap.BootstrapCreatedEvidence;
using static IdentityPlatform.Administrator.Tests.Features.Bootstrap.BootstrapEvidence;
using static IdentityPlatform.Administrator.Tests.Features.Bootstrap.BootstrapSnapshot;

namespace IdentityPlatform.Administrator.Tests.Features.Bootstrap;

/// <summary>Real process contention and atomic rollback scenarios.</summary>
[Collection("administrator-bootstrap")]
public sealed class BootstrapAtomicityTests(BootstrapFixture fixture)
{
    private const string IdenticalOperation = "identical";
    private const int ContendingProcesses = 2;
    private const string CredentialDisclosureFailure = "Bootstrap output disclosed a credential.";
    private const string InitializationMarker = "enrollment initialized";
    private const string MissingIdenticalReconciliation = "Identical operation did not reconcile.";
    private const string ReconciliationMarker = "already initialized";
    private const string FailedWinnerReconciliation = "Winner reconciliation failed.";
    private const string ChangedWinnerState = "Winner reconciliation changed persisted state.";
    private const string PartialStateAfterFailure = "Failed initialization left partial state.";
    private const string CancellationClassificationFailure = "Cancellation classification failed after drain/rollback/recovery proof: ";

    private static readonly string[] Races = ["identical", "conflicting"];
    private static readonly string[] Failures = ["audit", "manager", "cancel-lock", "kill-precommit", "cancel-manager"];
    public static IEnumerable<TheoryDataRow<string, string>> RaceCases => Cases(Races);
    public static IEnumerable<TheoryDataRow<string, string>> FailureCases => Cases(Failures);

    /// <summary>Proves real process contention, the winning receipt and unchanged reconciliation.</summary>
    [Theory]
    [MemberData(nameof(RaceCases))]
    public async Task Given_IndependentProcesses_When_BootstrapOperationsContend_Then_OneInitializationAndExactReconciliationOccur(string provider, string scenario)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        var secondOperation = scenario == IdenticalOperation ? lease.OperationId : Guid.NewGuid();
        await using var first = new BootstrapProcess(provider, lease.RuntimeConnection);
        await using var second = new BootstrapProcess(provider, lease.RuntimeConnection);
        await using (var held = await GovernanceLock.AcquireAsync(lease))
        {
            await first.SendAsync(lease.Input());
            await second.SendAsync(lease.Input(operation: secondOperation));
            await held.ObserveWaitersAsync(lease, ContendingProcesses);
        }
        var results = new[] { await first.CompleteAsync(), await second.CompleteAsync() };
        Assert.True(results.All(result => !result.Output.Contains(lease.Password, StringComparison.Ordinal)), CredentialDisclosureFailure);
        Assert.Equal(1, results.Count(result => result.ExitCode == 0 && result.Output.Contains(InitializationMarker, StringComparison.Ordinal)));
        if (scenario == IdenticalOperation)
        {
            Assert.True(results.All(result => result.ExitCode == 0), MissingIdenticalReconciliation);
            Assert.Equal(1, results.Count(result => result.Output.Contains(ReconciliationMarker, StringComparison.Ordinal)));
        }
        else
        {
            Assert.Equal(1, results.Count(result => result.ExitCode == 1));
        }
        var winner = results[0].Output.Contains(InitializationMarker, StringComparison.Ordinal) ? lease.OperationId : secondOperation;
        await AssertCreatedAsync(lease, lease.Password, winner);
        var before = await SnapshotAsync(lease);
        var replay = await RunAsync(lease, provider, lease.Input(operation: winner));
        Assert.True(replay.ExitCode == 0 && replay.Output.Contains(ReconciliationMarker, StringComparison.Ordinal), FailedWinnerReconciliation);
        Assert.True(before == await SnapshotAsync(lease), ChangedWinnerState);
    }

    /// <summary>Requires drain, complete rollback and successful recovery before accepting cancellation classification.</summary>
    [Theory]
    [MemberData(nameof(FailureCases))]
    public async Task Given_BootstrapInitialization_When_StorageOrProcessIsInterrupted_Then_AllWritesRollbackAndRecoverySucceeds(string provider, string scenario)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        var before = await SnapshotAsync(lease);
        var cancellationFailure = await BootstrapFailureScenarios.RunFailureAsync(lease, provider, scenario);
        await lease.AssertRuntimeDrainedAsync();
        Assert.True(before == await SnapshotAsync(lease), PartialStateAfterFailure);
        Assert.Equal(BootstrapOutcome.Created, await lease.InitializeAsync(Token));
        Assert.True(cancellationFailure is null, CancellationClassificationFailure + cancellationFailure);
    }
}
