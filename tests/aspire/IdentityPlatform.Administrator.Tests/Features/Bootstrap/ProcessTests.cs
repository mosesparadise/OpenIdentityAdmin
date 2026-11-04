using System.Text;
using IdentityPlatform.Administrator.Tests.Fixtures;
using static IdentityPlatform.Administrator.Tests.Features.Bootstrap.BootstrapEvidence;
using static IdentityPlatform.Administrator.Tests.Features.Bootstrap.BootstrapSnapshot;

namespace IdentityPlatform.Administrator.Tests.Features.Bootstrap;

/// <summary>Real command framing, lifecycle and configuration scenarios.</summary>
[Collection("administrator-bootstrap")]
public sealed class BootstrapProcessTests(BootstrapFixture fixture)
{
    private const string EscapedHighScenario = "escaped-high";
    private const string EscapedHighInput = "{\"password\":\"\\uD800\"}";
    private const string EscapedLowScenario = "escaped-low";
    private const string EscapedLowInput = "{\"password\":\"\\uDC00\"}";
    private const string InvalidUtf8Scenario = "invalid-utf8";
    private const byte InvalidUtf8LeadByte = 0xFF;
    private const byte InvalidUtf8ContinuationByte = 0xFE;
    private const string TooLargeScenario = "too-large";
    private const int OversizedInputLength = 4097;
    private const string InvalidCanaryScenario = "invalid-canary";
    private const string CanaryInput = "{\"password\":\"canary-secret-value\",\"unknown\":true}";
    private const string MissingEofScenario = "missing-eof";
    private const int InvalidInputExitCode = 2;
    private const string CanarySecret = "canary-secret-value";
    private const string DisclosedPipeInput = "Input appeared in process diagnostics.";
    private const string ChangedInvalidInputState = "Invalid or incomplete input wrote database state.";
    private const string MissingConnectionScenario = "missing-connection";
    private const string OperationScenario = "operation";
    private const string UnknownOperation = "unknown-operation";
    private const string InitializeOperation = "initialize-administrator";
    private const string LocalRolesScenario = "local-roles";
    private const string DevelopmentEnvironment = "Development";
    private const string ProductionEnvironment = "Production";
    private const string TerminalScenario = "terminal";
    private const string InvalidInputMessage = "Invalid administrator bootstrap input.";
    private const string InvalidConfigurationMessage = "Invalid migration configuration.";
    private const string WrongConfigurationBoundary = "Failure was not attributed to the expected command boundary.";
    private const string ConfigurationDisclosedCredentials = "Rejected command or PTY echoed credentials.";
    private const string ChangedConfigurationState = "Rejected operation configuration changed persisted state.";

    private static readonly string[] Processes = ["lost-result", "old-schema", "runtime-only", "unrelated-data", "upgrade-only"];
    private static readonly string[] Inputs = ["invalid-utf8", "too-large", "missing-eof", "invalid-canary", "escaped-high", "escaped-low"];
    public static IEnumerable<TheoryDataRow<string, string>> ProcessCases => Cases(Processes);
    public static IEnumerable<TheoryDataRow<string, string>> InputCases => Cases(Inputs);

    /// <summary>Checks native restart, schema, privilege and upgrade boundaries.</summary>
    [Theory]
    [MemberData(nameof(ProcessCases))]
    public async Task Given_BootstrapProcess_When_RestartSchemaOrPrivilegeBoundaryIsExercised_Then_PersistedStateIsPreserved(string provider, string scenario)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        await BootstrapProcessScenarios.RunScenarioAsync(lease, provider, scenario);
    }

    /// <summary>Rejects malformed or incomplete real pipe input without state changes or disclosure.</summary>
    [Theory]
    [MemberData(nameof(InputCases))]
    public async Task Given_BootstrapPipeInput_When_InvalidOrIncomplete_Then_NoCredentialOrWriteEscapes(string provider, string scenario)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        var before = await SnapshotAsync(lease);
        await using var process = new BootstrapProcess(provider, lease.RuntimeConnection);
        var input = scenario switch
        {
            EscapedHighScenario => Encoding.UTF8.GetBytes(EscapedHighInput),
            EscapedLowScenario => Encoding.UTF8.GetBytes(EscapedLowInput),
            InvalidUtf8Scenario => new byte[] { InvalidUtf8LeadByte, InvalidUtf8ContinuationByte },
            TooLargeScenario => Encoding.UTF8.GetBytes(new string(' ', OversizedInputLength)),
            InvalidCanaryScenario => Encoding.UTF8.GetBytes(CanaryInput),
            _ => lease.Input()
        };
        await process.SendAsync(input, close: scenario != MissingEofScenario);
        var result = await process.CompleteAsync();
        Assert.Equal(scenario == MissingEofScenario ? 1 : InvalidInputExitCode, result.ExitCode);
        Assert.False(result.Output.Contains(lease.Password, StringComparison.Ordinal) || result.Output.Contains(CanarySecret, StringComparison.Ordinal), DisclosedPipeInput);
        Assert.True(before == await SnapshotAsync(lease), ChangedInvalidInputState);
    }

    private static readonly string[] Configuration = ["operation", "local-roles", "missing-connection", "terminal"];
    public static IEnumerable<TheoryDataRow<string, string>> ConfigurationCases => Cases(Configuration);

    /// <summary>Rejects unsafe real process configuration before writing or disclosing credentials.</summary>
    [Theory]
    [MemberData(nameof(ConfigurationCases))]
    public async Task Given_BootstrapConfiguration_When_UnsafeOperationIsRequested_Then_CommandRejectsBeforeWriting(string provider, string scenario)
    {
        await using var lease = await fixture.LeaseAsync(provider);
        var before = await SnapshotAsync(lease);
        await using var process = new BootstrapProcess(provider, scenario == MissingConnectionScenario ? "" : lease.RuntimeConnection,
            operation: scenario == OperationScenario ? UnknownOperation : InitializeOperation,
            environment: scenario == LocalRolesScenario ? DevelopmentEnvironment : ProductionEnvironment,
            sessionConnection: scenario == OperationScenario ? lease.AdministratorConnection : null, provision: scenario == LocalRolesScenario, terminal: scenario == TerminalScenario);
        // Otherwise valid input prevents malformed-input rejection from masking a missing guard.
        await process.SendAsync(lease.Input());
        var result = await process.CompleteAsync();
        Assert.Equal(InvalidInputExitCode, result.ExitCode);
        var expected = scenario == TerminalScenario ? InvalidInputMessage : InvalidConfigurationMessage;
        Assert.True(result.Output.Contains(expected, StringComparison.Ordinal), WrongConfigurationBoundary);
        Assert.False(result.Output.Contains(lease.Password, StringComparison.Ordinal), ConfigurationDisclosedCredentials);
        Assert.True(before == await SnapshotAsync(lease), ChangedConfigurationState);
    }
}
