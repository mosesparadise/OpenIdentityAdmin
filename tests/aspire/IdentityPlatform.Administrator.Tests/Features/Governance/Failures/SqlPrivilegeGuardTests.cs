using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Failures;

/// <summary>Pure controls for SQL evidence handling; these rows do not claim native database verification.</summary>
[Trait("Provider", "SqlServer"), Trait("Diagnostic", "PrivilegeGuard")]
public sealed class SqlPrivilegeGuardTests
{
    private static readonly int[] NativeCodes = [229, 4902, 1088];
    private const int ObjectUnavailable = 1088;

    /// <summary>Repeats every incomplete-evidence control against both native denial codes and ambiguous object error 1088.</summary>
    public static IEnumerable<TheoryDataRow<SqlPrivilegeGuardCase, int>> Guards =>
        from condition in Enum.GetValues<SqlPrivilegeGuardCase>()
        from code in NativeCodes
        select new TheoryDataRow<SqlPrivilegeGuardCase, int>(condition, code);

    /// <summary>Independently removes one required observation and verifies that even a native denial cannot bypass it.</summary>
    [Theory, MemberData(nameof(Guards))]
    public void Given_IncompleteSqlEvidence_When_DenialIsRequired_Then_EveryNativeCodeFailsClosed(SqlPrivilegeGuardCase condition, int code)
    {
        var observed = new PrivilegeDiagnostics(new(code == ObjectUnavailable ? AlterOutcome.ProviderError : AlterOutcome.Denied, code),
            ProbeOutcome.Absent, null, null);
        Assert.NotNull(Record.Exception(() => observed.RequireSqlDenial(SqlPrivilegeGuardControls.Without(condition))));
    }

    /// <summary>Complete same-attempt evidence accepts only the expected raw native classifications without rewriting them.</summary>
    [Theory, InlineData(229), InlineData(4902), InlineData(1088)]
    public void Given_CompleteSqlEvidence_When_DenialIsRequired_Then_RawNativeClassificationIsPreserved(int code)
    {
        var alter = new AlterObservation(code == ObjectUnavailable ? AlterOutcome.ProviderError : AlterOutcome.Denied, code);
        var observed = new PrivilegeDiagnostics(alter, ProbeOutcome.Absent, null, null);
        observed.RequireSqlDenial(SqlPrivilegeGuardControls.Complete());
        Assert.Same(alter, observed.Alter);
    }

    /// <summary>Rejects success, cancellation, unknown errors and contradictions between provider code and classification.</summary>
    [Theory, InlineData("Succeeded", null), InlineData("Cancelled", 1088), InlineData("Unknown", 1088),
        InlineData("ProviderError", 229), InlineData("ProviderError", 4902), InlineData("ProviderError", -2),
        InlineData("Denied", 1088), InlineData("Denied", null), InlineData("Denied", -2)]
    public void Given_InvalidAlterResult_When_SqlEvidenceIsComplete_Then_DenialFails(string outcome, int? code)
    {
        var observed = new PrivilegeDiagnostics(new(Enum.Parse<AlterOutcome>(outcome), code), ProbeOutcome.Absent, null, null);
        Assert.NotNull(Record.Exception(() => observed.RequireSqlDenial(SqlPrivilegeGuardControls.Complete())));
    }

    /// <summary>No missing, failed, cancelled or contradictory independent probe can establish unchanged schema.</summary>
    [Theory, InlineData("Unknown"), InlineData("Present"), InlineData("InvalidGovernance"),
        InlineData("Failed"), InlineData("Cancelled"), InlineData("NotObserved")]
    public void Given_IncompleteProbe_When_SqlEvidenceOtherwiseMatches_Then_DenialFails(string probe)
    {
        var observed = new PrivilegeDiagnostics(new(AlterOutcome.ProviderError, ObjectUnavailable), Enum.Parse<ProbeOutcome>(probe), null, null);
        Assert.NotNull(Record.Exception(() => observed.RequireSqlDenial(SqlPrivilegeGuardControls.Complete())));
    }

    /// <summary>Metadata failure or cancellation remains primary, and the independent probe is still attempted exactly once.</summary>
    [Theory, InlineData(false), InlineData(true)]
    public async Task Given_MetadataReadFailure_When_ProbeAlsoFails_Then_PrimaryFailureRemainsAuthoritative(bool cancelled)
    {
        Exception primary = cancelled ? new OperationCanceledException() : new InvalidOperationException();
        var secondary = new InvalidOperationException();
        var probes = 0;
        var observed = await PrivilegeDiagnostics.ObserveAsync(() => Task.FromException<AlterObservation>(primary), () =>
        {
            probes++;
            return Task.FromException<ProbeOutcome>(secondary);
        });
        Assert.Equal(1, probes);
        Assert.Same(secondary, observed.ProbeFailure?.SourceException);
        Assert.Same(primary, Record.Exception(() => observed.RequireSqlDenial(null)));
    }

    /// <summary>A stored probe exception cannot be hidden by otherwise successful composite evidence.</summary>
    [Fact]
    public void Given_ProbeException_When_ObservationsOtherwiseMatch_Then_ProbeFailureIsRetained()
    {
        var failure = new InvalidOperationException();
        var observed = new PrivilegeDiagnostics(new(AlterOutcome.ProviderError, ObjectUnavailable), ProbeOutcome.Absent, null,
            ExceptionDispatchInfo.Capture(failure));
        Assert.Same(failure, Record.Exception(() => observed.RequireSqlDenial(SqlPrivilegeGuardControls.Complete())));
    }
}

/// <summary>Each seeded control invalidates one independent part of the same-attempt SQL proof.</summary>
public enum SqlPrivilegeGuardCase
{
    MissingEvidence, MissingRuntime, MissingAdministrator, RuntimeModel, AdministratorModel,
    EmptyDatabase, EmptyServer, DifferentDatabase, DifferentServer, MissingRuntimeObject, MissingAdministratorObject,
    NonpositiveObject, DifferentObject, WrongPrincipal, UnknownRuntimePermission, GrantedRuntimePermission,
    UnknownAdministratorPermission, DeniedAdministratorPermission, RuntimeProbePresent, AdministratorProbePresent,
    MissingRuntimeSingleton, MissingAdministratorSingleton, EmptyRuntimeConnection, ChangedBeforeConnection,
    ChangedAfterConnection, ClosedBeforeConnection, ClosedAfterConnection, NoAlterAttempt
}
