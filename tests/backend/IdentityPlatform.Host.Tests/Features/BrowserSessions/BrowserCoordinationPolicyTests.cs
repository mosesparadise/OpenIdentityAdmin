using IdentityPlatform.Infrastructure.Features.Sessions.Browser;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Checks tick-precise original operation bounds and canonical lookup representations without simulating provider acceptance.</summary>
public sealed class BrowserCoordinationPolicyTests
{
    private const long Start = 1000 * TimeSpan.TicksPerSecond;
    private const long Observed = 1100 * TimeSpan.TicksPerSecond;
    private const long Expiry = 1300 * TimeSpan.TicksPerSecond;

    /// <summary>Uses independent before/equality/after vectors for expiry, rollback and immutable monotonic observation.</summary>
    [Theory]
    [InlineData(10699999999, false)]
    [InlineData(10700000000, true)]
    [InlineData(12999999999, true)]
    [InlineData(13000000000, false)]
    [InlineData(13000000001, false)]
    public void Given_OriginalIntent_When_TimeIsCompared_Then_EqualityAndExcessiveRollbackDeny(long now, bool expected) =>
        Assert.Equal(expected, BrowserSessionPolicy.Current(Start, Observed, Expiry, now));

    /// <summary>Empty, lowercase, padded, nonhex and wrong-length identifiers cannot reach ordinal storage lookup.</summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", true)]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", false)]
    [InlineData("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEG", false)]
    public void Given_DigestRepresentation_When_Validated_Then_OnlyCanonicalUppercaseSha256IsAccepted(string? value, bool expected) =>
        Assert.Equal(expected, BrowserSessionPolicy.IsDigest(value));

    /// <summary>Neither nonpositive generations, mismatched revisions nor an expired matching context grant acceptance.</summary>
    [Theory]
    [InlineData(0, 1, 11000000000, false)]
    [InlineData(1, 0, 11000000000, false)]
    [InlineData(2, 1, 11000000000, false)]
    [InlineData(1, 1, 11000000000, true)]
    [InlineData(1, 1, 13000000000, false)]
    public void Given_ContextGeneration_When_Checked_Then_ExactLivePositiveGenerationIsRequired(long expected, long revision, long now, bool accepted)
    {
        var row = new BrowserContext { CorrelationId = Guid.NewGuid(), Generation = 1, Revision = revision, ObservedAt = Observed, ExpiresAt = Expiry };
        Assert.Equal(accepted, BrowserSessionPolicy.Matches(row, expected, now));
        Assert.Equal(Observed, row.ObservedAt);
        Assert.Equal(Expiry, row.ExpiresAt);
    }
}
