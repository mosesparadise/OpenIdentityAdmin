using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.Accounts;

/// <summary>Validates the independent Base32 enrollment oracle against published RFC 6238 SHA-1 vectors.</summary>
public sealed class OracleTests
{
    private const string PublishedBase32Seed = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
    private static readonly (long Time, string Code)[] Vectors = [(59, "287082"), (1111111109, "081804"),
        (1111111111, "050471"), (1234567890, "005924"), (2000000000, "279037"), (20000000000, "353130")];

    /// <summary>Both provider obligations use a separately verified decoder and dynamic truncation oracle, independent of production Identity code.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public void Given_PublishedBase32Vectors_When_EnrollmentOracleRuns_Then_AllExpectedNativeWidthCodesMatch(string provider)
    {
        Assert.False(string.IsNullOrWhiteSpace(provider));
        foreach (var vector in Vectors)
        {
            Assert.Equal(vector.Code, FactorOracle.Code(PublishedBase32Seed, vector.Time));
        }
        RequireUnusedCurrentCounter();
    }

    /// <summary>Fixed independent values prove current-counter fallback, unchanged priority and rejection when every native-window value was consumed.</summary>
    private static void RequireUnusedCurrentCounter()
    {
        const long ObservedAt = 1234567890;
        const string Current = "005924";
        const string FirstAdjacent = "590587";
        const string PreviousCounter = "980357";
        const string SecondNextCounter = "240500";
        const string SecondPreviousCounter = "186057";
        HashSet<string> accepted = new([FirstAdjacent, PreviousCounter, SecondNextCounter, SecondPreviousCounter], StringComparer.Ordinal);
        var selected = FactorOracle.UnusedCode(PublishedBase32Seed, accepted, ObservedAt);
        Assert.Equal(Current, selected);
        Assert.DoesNotContain(selected, accepted);
        accepted.Add(Current);
        Assert.Throws<Xunit.Sdk.TrueException>(() => FactorOracle.UnusedCode(PublishedBase32Seed, accepted, ObservedAt));
        Assert.Equal(FirstAdjacent, FactorOracle.UnusedCode(PublishedBase32Seed, new HashSet<string>(StringComparer.Ordinal), ObservedAt));
    }
}
