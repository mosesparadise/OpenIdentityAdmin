using IdentityPlatform.Administrator.Tests.Fixtures.Governance;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Failures;

/// <summary>Seeded diagnostic controls run no database and cannot stand in for actual runtime-principal evidence.</summary>
[Trait("Diagnostic", "Privilege")]
public sealed class PrivilegeDiagnosticTests
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private const string PrimaryAndFailure = "primary-and-failure";
    private const string PrimaryAndCancellation = "primary-and-cancellation";
    private const string CancellationAndFailure = "cancellation-and-failure";
    private const string DdlAbsent = "ddl-absent";
    private const string DdlPresent = "ddl-present";
    private const string DeniedFailed = "denied-failed";
    private const string DeniedUnobserved = "denied-unobserved";
    private const string DeniedAbsent = "denied-absent";

    public static IEnumerable<TheoryDataRow<string, string>> Failures => GovernanceRows.For(PrimaryAndFailure, PrimaryAndCancellation, CancellationAndFailure);
    public static IEnumerable<TheoryDataRow<string, string>> Outcomes => GovernanceRows.For(DdlAbsent, DdlPresent, DeniedFailed, DeniedUnobserved, DeniedAbsent);

    /// <summary>A failed or cancelled probe runs once and retains rather than masks an already failed or cancelled ALTER.</summary>
    [Theory, MemberData(nameof(Failures))]
    public async Task Given_PrimaryAndProbeFailures_When_DiagnosticsUnwind_Then_PrimaryExceptionAndSecondaryEvidenceRemain(string provider, string condition)
    {
        Assert.Contains(provider, Providers);
        Exception primary = condition == CancellationAndFailure ? new OperationCanceledException() : new InvalidOperationException();
        Exception secondary = condition == PrimaryAndCancellation ? new OperationCanceledException() : new InvalidOperationException();
        var probes = 0;
        var observed = await PrivilegeDiagnostics.ObserveAsync(() => Task.FromException<AlterObservation>(primary), () =>
        {
            probes++;
            return Task.FromException<ProbeOutcome>(secondary);
        });
        Assert.Equal(1, probes);
        Assert.Same(primary, observed.PrimaryFailure?.SourceException);
        Assert.Same(secondary, observed.ProbeFailure?.SourceException);
        Assert.Equal(condition == PrimaryAndCancellation ? ProbeOutcome.Cancelled : ProbeOutcome.Failed, observed.Probe);
        Assert.Same(primary, Record.Exception(observed.RequireDenial));
    }

    /// <summary>Successful DDL or incomplete metadata never becomes denial; even a valid denial requires a completed absent-column probe.</summary>
    [Theory, MemberData(nameof(Outcomes))]
    public async Task Given_AlterAndProbeOutcomes_When_DenialIsRequired_Then_OnlyNativeDenialAndVerifiedAbsencePass(string provider, string condition)
    {
        Assert.Contains(provider, Providers);
        var alter = condition is DdlAbsent or DdlPresent ? AlterOutcome.Succeeded : AlterOutcome.Denied;
        var probe = condition switch
        {
            DdlPresent => ProbeOutcome.Present,
            DeniedUnobserved => ProbeOutcome.NotObserved,
            _ => ProbeOutcome.Absent,
        };
        var observed = await PrivilegeDiagnostics.ObserveAsync(() => Task.FromResult(new AlterObservation(alter, null)),
            () => condition == DeniedFailed ? Task.FromException<ProbeOutcome>(new InvalidOperationException()) : Task.FromResult(probe));
        var failure = Record.Exception(observed.RequireDenial);
        if (condition == DeniedAbsent)
        {
            Assert.Null(failure);
        }
        else
        {
            Assert.NotNull(failure);
        }
        Assert.Equal(alter, observed.Alter.Outcome);
        if (condition == DeniedFailed)
        {
            var cancellation = new OperationCanceledException();
            var cancelled = await PrivilegeDiagnostics.ObserveAsync(() => Task.FromResult(new AlterObservation(AlterOutcome.Denied, null)),
                () => Task.FromException<ProbeOutcome>(cancellation));
            Assert.Equal(ProbeOutcome.Cancelled, cancelled.Probe);
            Assert.Same(cancellation, Record.Exception(cancelled.RequireDenial));
        }
    }
}
