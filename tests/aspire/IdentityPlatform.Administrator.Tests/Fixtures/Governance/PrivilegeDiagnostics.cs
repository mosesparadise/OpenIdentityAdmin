using System.Runtime.ExceptionServices;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Retains independent primary/probe outcomes; observations cannot confer native permission denial.</summary>
internal sealed record PrivilegeDiagnostics(AlterObservation Alter, ProbeOutcome Probe,
    ExceptionDispatchInfo? PrimaryFailure, ExceptionDispatchInfo? ProbeFailure)
{
    private const string DenialMessage = "Expected native ALTER permission denial; raw provider diagnostics are withheld.";
    private const string OutputFormat = "governance-privilege {0} {1} {2}";
    private const string MissingCode = "none";
    private const int MaximumDiagnosticCode = 999999;

    /// <summary>Always attempts the independent probe, retaining primary exceptions even when cleanup fails or cancels.</summary>
    internal static async Task<PrivilegeDiagnostics> ObserveAsync(Func<Task<AlterObservation>> alter, Func<Task<ProbeOutcome>> probe)
    {
        var primary = new AlterObservation(AlterOutcome.Unknown, null);
        var secondary = ProbeOutcome.Unknown;
        ExceptionDispatchInfo? primaryFailure = null, probeFailure = null;
        try
        {
            primary = await alter();
        }
        catch (Exception exception) { primaryFailure = ExceptionDispatchInfo.Capture(exception); }
        finally
        {
            try
            {
                secondary = await probe();
            }
            catch (OperationCanceledException exception)
            {
                secondary = ProbeOutcome.Cancelled;
                probeFailure = ExceptionDispatchInfo.Capture(exception);
            }
            catch (Exception exception)
            {
                secondary = ProbeOutcome.Failed;
                probeFailure = ExceptionDispatchInfo.Capture(exception);
            }
        }
        return new PrivilegeDiagnostics(primary, secondary, primaryFailure, probeFailure);
    }

    /// <summary>Preserves original assertion ordering: an unsuccessful ALTER oracle dominates any secondary probe failure.</summary>
    internal void RequireDenial()
    {
        PrimaryFailure?.Throw();
        Assert.True(Alter.Outcome == AlterOutcome.Denied, DenialMessage);
        ProbeFailure?.Throw();
        Assert.Equal(ProbeOutcome.Absent, Probe);
    }

    /// <summary>Emits only finite outcome labels and a bounded SQL numeric code; output failure cannot alter test behavior.</summary>
    internal void Write()
    {
        var alter = Enum.IsDefined(Alter.Outcome) ? Alter.Outcome : AlterOutcome.Unknown;
        var probe = Enum.IsDefined(Probe) ? Probe : ProbeOutcome.Unknown;
        var code = Alter.ProviderCode is >= -MaximumDiagnosticCode and <= MaximumDiagnosticCode
            ? Alter.ProviderCode.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : MissingCode;
        try
        {
            TestContext.Current.TestOutputHelper?.WriteLine(OutputFormat, alter, probe, code);
        }
        catch (Exception) { /* Diagnostic output must not mask the authoritative assertion. */ }
    }
}

/// <summary>Closed classifications of the actual runtime-principal ALTER attempt.</summary>
internal enum AlterOutcome
{
    Unknown, Succeeded, Denied, ProviderError, Cancelled
}

/// <summary>Closed classifications from independently visible schema and governance metadata.</summary>
internal enum ProbeOutcome
{
    Unknown, Absent, Present, InvalidGovernance, Failed, Cancelled, NotObserved
}

/// <summary>Carries only the finite outcome and optional native numeric SQL code, never raw provider diagnostics.</summary>
internal sealed record AlterObservation(AlterOutcome Outcome, int? ProviderCode);
