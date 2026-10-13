using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityPlatform.AppHost.Tests.Features;

[Trait("Category", "Harness")]
public sealed class ProfileTransitionLogTests
{
    private const string Worker = "worker";
    private const string Auth = "auth";
    private const string Canary = "synthetic-private-metadata";
    private const string ProcessIdProperty = "executable.pid";
    private const string ProjectKind = "project";
    private const int FailureExitCode = 2;
    private const string ObserverFailureMarker = "observer-failed=True";
    private const int ExamplePid = 1234;
    private const int SaturationCount = 140;
    private const string FirstAuth = "profile-first-unavailable role=auth";
    private const string ExpectedRuntime = "state=RuntimeUnhealthy";
    private const string FirstWorker = "profile-first-unavailable role=worker";
    private const string LastWorker = "profile-last-observed role=worker";

    /// <summary>Preserves the first unavailable observation independently when later state becomes healthy.</summary>
    [Theory]
    [InlineData("Finished")]
    [InlineData("Exited")]
    [InlineData("FailedToStart")]
    [InlineData("RuntimeUnhealthy")]
    public void Given_UnavailableThenHealthy_When_Recorded_Then_FirstUnavailableIsNotOverwritten(string state)
    {
        var log = new ProfileTransitionLog();
        log.Record(Worker, Snapshot(state, HealthStatus.Unhealthy));
        log.Record(Worker, Snapshot(KnownResourceStates.Running, HealthStatus.Healthy));
        var lines = log.Lines();
        Assert.Contains("sequence=1", Assert.Single(lines, line => line.StartsWith(FirstWorker, StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Contains("sequence=2", Assert.Single(lines, line => line.StartsWith(LastWorker, StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Contains("state=Running", Assert.Single(lines, line => line.StartsWith(LastWorker, StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    /// <summary>Running readiness failure is distinct from unavailable process state; equal safe metadata is deduplicated.</summary>
    [Fact]
    public void Given_RunningUnhealthy_When_RepeatedWithPrivateChanges_Then_NoTerminalStateOrDuplicateIsInvented()
    {
        var log = new ProfileTransitionLog();
        var snapshot = Snapshot(KnownResourceStates.Running, HealthStatus.Unhealthy);
        log.Record(Worker, snapshot);
        log.Record(Worker, snapshot with
        {
            Properties = [new(Canary, new HostileValue())]
        });
        var lines = log.Lines();
        Assert.StartsWith("profile-transitions observed=1 retained=1 truncated=False observer-failed=False", lines[0], StringComparison.Ordinal);
        Assert.Contains("profile-first-unavailable role=worker not-observed", lines);
        Assert.DoesNotContain(lines, line => line.Contains(Canary, StringComparison.Ordinal));
    }

    /// <summary>Retains scalar and phase changes while redacting invalid state, health, PID and unknown resource roles.</summary>
    [Fact]
    public void Given_HostileAndChangingMetadata_When_Recorded_Then_OnlyFixedRolesAndSafeScalarsEscape()
    {
        var log = new ProfileTransitionLog();
        var snapshot = Snapshot(Canary, (HealthStatus)int.MaxValue) with
        {
            Properties = [new(ProcessIdProperty, new HostileValue())]
        };
        log.Record(Canary, snapshot);
        log.Record(Worker, snapshot);
        log.Record(Worker, snapshot with
        {
            Properties = [new(ProcessIdProperty, ExamplePid)],
            ExitCode = FailureExitCode
        });
        log.Record(Worker, Snapshot(KnownResourceStates.Running, HealthStatus.Healthy));
        log.SetPhase(ProfilePhase.HostReadiness);
        log.Record(Worker, Snapshot(KnownResourceStates.Running, HealthStatus.Healthy));
        const string expectedRedacted = "state=unknown exit=unavailable pid=unavailable health=unknown";
        const string expectedNumbers = "exit=2 pid=1234";
        const string expectedPhase = "phase=HostReadiness";
        var lines = log.Lines();
        Assert.StartsWith("profile-transitions observed=4 retained=4", lines[0], StringComparison.Ordinal);
        Assert.Contains(lines, line => line.Contains(expectedRedacted, StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains(expectedNumbers, StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains(expectedPhase, StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains(Canary, StringComparison.Ordinal));
    }

    /// <summary>Bounds retained output while independently capturing the first Worker failure after history saturation.</summary>
    [Fact]
    public void Given_SaturatedHistory_When_WorkerFailsThenRecovers_Then_TruncationAndFirstFailureRemainVisible()
    {
        var log = new ProfileTransitionLog();
        for (var index = 0; index < SaturationCount; index++)
        {
            log.Record(Worker, Snapshot(KnownResourceStates.Running, HealthStatus.Healthy) with
            {
                ExitCode = index
            });
        }
        log.Record(Worker, Snapshot(KnownResourceStates.FailedToStart, HealthStatus.Unhealthy));
        log.Record(Worker, Snapshot(KnownResourceStates.Running, HealthStatus.Healthy));
        log.Record(Auth, Snapshot(KnownResourceStates.RuntimeUnhealthy, HealthStatus.Unhealthy));
        log.Record(Auth, Snapshot(KnownResourceStates.Running, HealthStatus.Healthy));
        var lines = log.Lines();
        Assert.Contains(lines, line => line.StartsWith(FirstAuth, StringComparison.Ordinal) && line.Contains(ExpectedRuntime, StringComparison.Ordinal));
        Assert.Equal(141, lines.Length);
        Assert.StartsWith("profile-transitions observed=144 retained=128 truncated=True", lines[0], StringComparison.Ordinal);
        Assert.Contains("sequence=141", Assert.Single(lines, line => line.StartsWith(FirstWorker, StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Contains("sequence=142", Assert.Single(lines, line => line.StartsWith(LastWorker, StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Contains("publication-drain=not-confirmed", lines[0], StringComparison.Ordinal);
    }

    /// <summary>Returns immutable output copies and explicit missing roles when no event was observed.</summary>
    [Fact]
    public void Given_NoEvents_When_ReadAndMarkedFailed_Then_MissingEvidenceAndFailureAreExplicit()
    {
        var log = new ProfileTransitionLog();
        var before = log.Lines();
        log.MarkFailed();
        Assert.False(before[0].Contains(ObserverFailureMarker, StringComparison.Ordinal));
        Assert.Contains("observer-failed=True", log.Lines()[0], StringComparison.Ordinal);
        Assert.Equal(13, before.Length);
        Assert.Contains("profile-last-observed role=worker not-observed", before);
    }

    /// <summary>Creates a minimal snapshot without arbitrary runtime configuration.</summary>
    private static CustomResourceSnapshot Snapshot(string state, HealthStatus health) => new CustomResourceSnapshot()
    {
        ResourceType = ProjectKind,
        Properties = [],
        State = new(state, null)
    }.WithHealthReports([new(Canary, health, Canary, Canary)]);

    private sealed class HostileValue
    {
        /// <summary>Fails if diagnostic formatting attempts arbitrary value conversion.</summary>
        public override string ToString() => throw new InvalidOperationException(Canary);
    }
}
