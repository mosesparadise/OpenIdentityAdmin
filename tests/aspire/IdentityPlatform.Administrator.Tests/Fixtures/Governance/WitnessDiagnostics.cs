namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Emits fixed checkpoints without waiting, changing race predicates or rewriting exceptions.</summary>
internal static class WitnessDiagnostics
{
    private const string OutputFormat = "governance-witness-stage {0}";

    /// <summary>Best-effort observation grants no acceptance and never masks a primary assertion or disposal failure.</summary>
    internal static void Write(WitnessStage stage)
    {
        if (!Enum.IsDefined(stage))
        {
            return;
        }
        try
        {
            TestContext.Current.TestOutputHelper?.WriteLine(OutputFormat, stage);
        }
        catch (Exception) { /* The test oracle remains authoritative if its diagnostic sink fails. */ }
    }
}

/// <summary>Names only reviewed witness-test phases, excluding all identities, SQL and credentials.</summary>
internal enum WitnessStage
{
    Setup, Prepared, GateWait, GateArrived, OrderWait, WriterFirst, ObserverFirst,
    WriterSecurity, OrderComplete, CommitResult, WriterJoined, Drain, Security, Authority, Usable, Oracle,
}
