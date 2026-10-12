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
    }
}
