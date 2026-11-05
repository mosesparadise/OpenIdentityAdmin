using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.AppHost.Tests.Features.LocalDemo;

/// <summary>Reproduces exhaustion of the five native counters without sleeping, submitting authentication or altering product time.</summary>
public sealed class LocalDemoFactorTests
{
    private const string PublishedSeed = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
    private const long Observation = 1234567890;
    private const int StepSeconds = 30;
    private const int NativeWindowCount = 5;
    private const string FirstPriorityCode = "590587";
    private const string RolloverCode = "992085";
    private static readonly string[] AcceptedWindow = ["186057", "980357", "005924", FirstPriorityCode, "240500"];

    /// <summary>A sixth ceremony needs new proof after a real rollover; every previously accepted numeric value remains excluded.</summary>
    [Fact, Trait("Category", "Harness")]
    public void Given_AllNativeWindowCodesAccepted_When_NextCounterArrives_Then_NewProofBecomesAvailableWithoutReplay()
    {
        var accepted = new HashSet<string>(AcceptedWindow, StringComparer.Ordinal);
        Assert.Equal(FirstPriorityCode, FactorOracle.UnusedCode(PublishedSeed, new HashSet<string>(StringComparer.Ordinal), Observation));
        Assert.Equal(NativeWindowCount, accepted.Count);
        Assert.Null(FactorOracle.AvailableCode(PublishedSeed, accepted, Observation));
        Assert.Throws<Xunit.Sdk.TrueException>(() => FactorOracle.UnusedCode(PublishedSeed, accepted, Observation));
        Assert.Equal(AcceptedWindow.Order(StringComparer.Ordinal), accepted.Order(StringComparer.Ordinal));
        var next = Observation + StepSeconds;
        var code = FactorOracle.UnusedCode(PublishedSeed, accepted, next);
        Assert.Equal(RolloverCode, code);
        Assert.DoesNotContain(code, accepted);
        accepted.Add(code);
        Assert.Null(FactorOracle.AvailableCode(PublishedSeed, accepted, next));
    }
}
