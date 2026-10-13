using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;

namespace IdentityPlatform.Administrator.Tests.Features.Governance.Failures;

/// <summary>Pure controls prove setup output is closed and cannot alter failure or cancellation behavior; no database is started.</summary>
[Trait("Diagnostic", "Setup")]
public sealed class SetupDiagnosticTests
{
    private const string Unknown = "unknown-stage";
    private const string Failed = "failed-sink";
    private const string Cancelled = "cancelled-sink";
    private const string Valid = "valid-stages";
    private const int UnknownStage = -1;
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    public static IEnumerable<TheoryDataRow<string, string>> FailureCases => GovernanceRows.For(Unknown, Failed, Cancelled);
    public static IEnumerable<TheoryDataRow<string, string>> ValidCases => GovernanceRows.For(Valid);

    /// <summary>Unknown enum values emit nothing; failed or cancelled sinks never replace the surrounding primary exception.</summary>
    [Theory, MemberData(nameof(FailureCases))]
    public void Given_InvalidStageOrFailedSink_When_PrimaryFailureUnwinds_Then_OriginalExceptionRemains(string provider, string condition)
    {
        Assert.Contains(provider, Providers);
        var primary = new InvalidOperationException();
        var calls = 0;
        Action<string> sink = _ =>
        {
            calls++;
            if (condition == Cancelled)
            {
                throw new OperationCanceledException();
            }
            throw new IOException();
        };
        Action operation = () =>
        {
            try
            {
                throw primary;
            }
            finally
            {
                SetupDiagnostics.Write(condition == Unknown ? (SetupStage)UnknownStage : SetupStage.HostBuild, sink);
            }
        };
        var failure = Record.Exception(operation);
        Assert.Same(primary, failure);
        Assert.Equal(condition == Unknown ? 0 : 1, calls);
    }

    /// <summary>Every declared checkpoint emits one finite value without a resource, identifier or free-text diagnostic field.</summary>
    [Theory, MemberData(nameof(ValidCases))]
    public void Given_ReviewedStages_When_Observed_Then_OutputContainsOnlyFiniteLabels(string provider, string condition)
    {
        Assert.Contains(provider, Providers);
        Assert.Equal(Valid, condition);
        var lines = new List<string>();
        foreach (var stage in Enum.GetValues<SetupStage>())
        {
            SetupDiagnostics.Write(stage, lines.Add);
        }
        Assert.Equal(23, lines.Count);
        Assert.Equal(23, lines.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("governance-setup-stage HostBuilder", lines[0]);
        Assert.Equal("governance-setup-stage CodePreparation", lines[^1]);
        Assert.All(lines, line => Assert.StartsWith("governance-setup-stage ", line, StringComparison.Ordinal));
    }
}
