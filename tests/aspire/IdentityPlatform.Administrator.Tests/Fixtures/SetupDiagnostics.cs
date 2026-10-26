namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Observes existing fixture phases without adding waits or retaining dynamic resource or credential data.</summary>
internal static class SetupDiagnostics
{
    private const string Prefix = "governance-setup-stage ";

    private static readonly System.Text.CompositeFormat FailureFormat = System.Text.CompositeFormat.Parse("bootstrap-startup-failure category={0} cancellation={1}");
    private const string Cancelled = "cancelled";
    private const string Timeout = "timeout";
    private const string Other = "other";

    /// <summary>Classifies only exception kind and cancellation state; messages, inner exceptions and resource values never enter output.</summary>
    internal static void Failure(Exception error, bool cancelled, Action<string>? sink = null)
    {
        try
        {
            var category = error is OperationCanceledException ? Cancelled : error is TimeoutException ? Timeout : Other;
            var line = string.Format(System.Globalization.CultureInfo.InvariantCulture, FailureFormat, category, cancelled);
            if (sink is null)
            {
                TestContext.Current.TestOutputHelper?.WriteLine(line);
            }
            else
            {
                sink(line);
            }
        }
        catch (Exception) { /* Observation never replaces the primary startup or cleanup outcome. */ }
    }

    /// <summary>Emits a closed enum label; a failed or cancelled output sink cannot replace the fixture's primary failure.</summary>
    internal static void Write(SetupStage stage, Action<string>? sink = null)
    {
        if (!Enum.IsDefined(stage))
        {
            return;
        }
        try
        {
            var line = Prefix + stage;
            if (sink is null)
            {
                TestContext.Current.TestOutputHelper?.WriteLine(line);
            }
            else
            {
                sink(line);
            }
        }
        catch (Exception) { /* Observation is best-effort and never changes fixture lifecycle behavior. */ }
    }
}

/// <summary>Finite checkpoints immediately before existing operations or after verified completion, never evidence of subsequent success.</summary>
internal enum SetupStage
{
    HostBuilder, HostBuild, HostStart, AuthReadiness, HostReady, HostCleanup,
    LeaseOpen, LeaseCreate, LeaseMigrate, LeaseProvision, LeaseReady, LeaseCleanup,
    JourneyLease, JourneyInitialize, JourneyServices, JourneyReady,
    EnrollmentStage, EnrollmentVerify, EnrollmentReady,
    WitnessCreate, WitnessSecurityPreparation, RequestPreparation, CodePreparation,
    AuthHealthy, LifecycleCaptured, ConnectionReady,
}
