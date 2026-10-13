namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Observes existing fixture phases without adding waits or retaining dynamic resource or credential data.</summary>
internal static class SetupDiagnostics
{
    private const string Prefix = "governance-setup-stage ";

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
}
