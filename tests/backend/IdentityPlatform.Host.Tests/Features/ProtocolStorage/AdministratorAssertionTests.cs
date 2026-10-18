using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Checks bounded replay inputs and strict time before the owning ledger can retain a digest.</summary>
public sealed class AdministratorAssertionTests
{
    private const string Identifier = "assertion-proof-01";
    private const long Now = 100;
    private const int LaterExpiryOffset = 2;
    private const string ExpectedDigest = "226566d9fe153f3c787d993a485ed9296f810d013fe4dc478dd538501818079a";

    /// <summary>The earlier post-native bounds reject expiry equality, future issue/not-before values and invalid lifetime shapes.</summary>
    [Theory]
    [InlineData(100, 160, null, true)]
    [InlineData(100, 100, null, false)]
    [InlineData(99, 100, null, false)]
    [InlineData(100, 161, null, false)]
    [InlineData(130, 190, null, true)]
    [InlineData(131, 191, null, false)]
    [InlineData(99, 159, 100L, true)]
    [InlineData(99, 159, 101L, false)]
    [InlineData(99, 159, 98L, false)]
    [InlineData(-1, 10, null, false)]
    [InlineData(long.MaxValue - 60, long.MaxValue, null, false)]
    public void Given_AssertionTimes_When_CheckedAtSerializedObservation_Then_StrictBoundsApply(long issued, long expires, long? notBefore, bool allowed)
    {
        var assertion = AdministratorAssertion.Create(Identifier, issued, expires, notBefore);
        Assert.Equal(allowed, assertion?.Allows(Now) == true);
    }

    /// <summary>Only a bounded ASCII identifier can contribute to the common endpoint-independent digest.</summary>
    [Theory]
    [InlineData(15, false)]
    [InlineData(16, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Given_IdentifierLength_When_Constructed_Then_RetentionInputIsBounded(int length, bool accepted) =>
        Assert.Equal(accepted, AdministratorAssertion.Create(new string('a', length), Now, Now + 1, null) is not null);

    /// <summary>Whitespace, punctuation and Unicode never alias an approved random ASCII proof identifier.</summary>
    [Theory]
    [InlineData("assertion proof-01")]
    [InlineData("assertion:proof-01")]
    [InlineData("assertion-proof-Ω1")]
    public void Given_NoncanonicalIdentifier_When_Constructed_Then_NoReplayInputIsCreated(string value) =>
        Assert.Null(AdministratorAssertion.Create(value, Now, Now + 1, null));

    /// <summary>An independent fixed vector pins length-prefix encoding and one shared identity across endpoint uses.</summary>
    [Fact]
    public void Given_SameIdentifierAtDifferentTimes_When_Hashed_Then_TheSharedDigestRemainsDomainBound()
    {
        Assert.Equal(ExpectedDigest, AdministratorAssertion.Create(Identifier, Now, Now + 1, null)!.Digest);
        Assert.Equal(ExpectedDigest, AdministratorAssertion.Create(Identifier, Now + 1, Now + LaterExpiryOffset, null)!.Digest);
    }
}
