using Aspire.Hosting.ApplicationModel;

namespace IdentityPlatform.AppHost.Tests.Features;

[Trait("Category", "Harness")]
public sealed class ProfileStartupEvidenceTests
{
    private static readonly string[] ExpectedFailedReadLines = new[]
        {
            "profile-resource=database snapshot-read-failed", "profile-resource=migrator snapshot=absent",
            "profile-resource=auth snapshot=absent", "profile-resource=admin-api snapshot=absent",
            "profile-resource=admin-bff snapshot=absent", "profile-resource=worker snapshot=absent"
        };
    private const string ProcessIdProperty = "executable.pid";
    private const string ProjectResourceType = "project";
    private const int ExampleProcessId = 1234;
    private const int FailureExitCode = 2;
    private const string Canary = "synthetic-secret-must-not-escape";

    /// <summary>Captures the failed process fields needed for investigation without arbitrary snapshot content.</summary>
    [Fact]
    public void Given_FailedResource_When_Described_Then_OnlySafeStateExitAndPidAreRetained()
    {
        var snapshot = new CustomResourceSnapshot
        {
            ResourceType = ProjectResourceType,
            Properties = [new(ProcessIdProperty, ExampleProcessId)],
            State = new(KnownResourceStates.FailedToStart, null),
            ExitCode = FailureExitCode
        };
        Assert.Equal("snapshot=present state=FailedToStart exit=2 pid=1234", ProfileStartupEvidence.Describe(snapshot));
        Assert.Equal("snapshot=absent", ProfileStartupEvidence.Describe(null));
    }

    /// <summary>Rejects hostile state text and arbitrary PID values without invoking object formatting.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData("synthetic-secret-must-not-escape")]
    [InlineData(long.MaxValue)]
    public void Given_UntrustedMetadata_When_Described_Then_UnknownValuesCannotEscape(object? value)
    {
        var snapshot = new CustomResourceSnapshot
        {
            ResourceType = Canary,
            Properties = [new(ProcessIdProperty, value), new(Canary, new HostileValue())],
            State = new(Canary, null)
        };
        Assert.Equal("snapshot=present state=unknown exit=unavailable pid=unavailable", ProfileStartupEvidence.Describe(snapshot));
        var hostile = snapshot with
        {
            Properties = [new(ProcessIdProperty, new HostileValue())]
        };
        Assert.Equal("snapshot=present state=unknown exit=unavailable pid=unavailable", ProfileStartupEvidence.Describe(hostile));
    }

    /// <summary>Attempts all six fixed roles despite a snapshot failure and preserves the primary exception instance.</summary>
    [Fact]
    public async Task Given_SnapshotReaderFailure_When_StartupFails_Then_RemainingRolesAreCapturedAndOriginalFailureSurvives()
    {
        var primary = new InvalidOperationException(Canary);
        var lines = new List<string>();
        var reads = 0;
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => ProfileStartupEvidence.ObserveAsync(
            () => Task.FromException(primary),
            _ => ++reads == 1 ? throw new IOException(Canary) : null,
            lines.Add));
        Assert.Same(primary, observed);
        Assert.Equal(6, reads);
        Assert.Equal(ExpectedFailedReadLines, lines);
    }

    /// <summary>Output failures cannot replace startup cancellation or prevent attempts for remaining resources.</summary>
    [Fact]
    public async Task Given_OutputFailure_When_StartupIsCancelled_Then_OriginalCancellationSurvives()
    {
        var primary = new OperationCanceledException();
        var writes = 0;
        var observed = await Assert.ThrowsAsync<OperationCanceledException>(() => ProfileStartupEvidence.ObserveAsync(
            () => Task.FromException(primary), _ => null, _ => { writes++; throw new IOException(Canary); }));
        Assert.Same(primary, observed);
        Assert.Equal(6, writes);
    }

    /// <summary>Successful graph checks do not query or log resource metadata.</summary>
    [Fact]
    public async Task Given_Success_When_Observed_Then_NoDiagnosticCallbacksRun()
    {
        await ProfileStartupEvidence.ObserveAsync(() => Task.CompletedTask,
            _ => throw new InvalidOperationException(Canary), _ => throw new InvalidOperationException(Canary));
    }

    private sealed class HostileValue
    {
        /// <summary>Fails if diagnostics attempt arbitrary object conversion.</summary>
        public override string ToString() => throw new InvalidOperationException(Canary);
    }
}
