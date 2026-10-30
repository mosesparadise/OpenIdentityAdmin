using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Host.Tests.Features.Applications;

/// <summary>Proves strong optimistic preconditions bind the exact resource and revision with no grammar aliases.</summary>
public sealed class ApplicationVersionTests
{
    private const int NextRevision = 2;
    private const int OverlongIdentifier = 401;
    private const char HighSurrogate = (char)0xd800;
    private const string DifferentCase = "ABC";
    private const string Composed = "é";
    private const string Decomposed = "e\u0301";
    private const string EmptyIdentifier = " ";
    private const string RevisionSuffix = "-1\"";
    private const string ExtraDigestPrefix = "\"0";
    private const string Quote = "\"";
    private const string Identifier = "abc";
    private const string Digest = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
    private const string FirstTag = "\"" + Digest + "-1\"";

    /// <summary>The independent SHA-256 vector fixes the wire format and prevents cross-resource or stale acceptance.</summary>
    [Fact]
    public void Given_CurrentResource_When_TagCreated_Then_OnlyExactResourceAndRevisionMatch()
    {
        Assert.Equal(FirstTag, ApplicationVersion.Create(Identifier, 1));
        Assert.True(ApplicationVersion.Matches(FirstTag, Identifier, 1));
        Assert.False(ApplicationVersion.Matches(FirstTag, Identifier, NextRevision));
        Assert.False(ApplicationVersion.Matches(FirstTag, DifferentCase, 1));
        Assert.NotEqual(ApplicationVersion.Create(Composed, 1), ApplicationVersion.Create(Decomposed, 1));
        Assert.True(ApplicationVersion.IsCanonical(ApplicationVersion.Create(Identifier, long.MaxValue)));
        Assert.True(ApplicationVersion.Matches(ApplicationVersion.Create(Identifier, long.MaxValue), Identifier, long.MaxValue));
    }

    /// <summary>Weak, multiple, whitespace and numeric aliases never become valid mutation preconditions.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("W/" + FirstTag)]
    [InlineData(" " + FirstTag)]
    [InlineData(FirstTag + " ")]
    [InlineData(FirstTag + "," + FirstTag)]
    [InlineData(Digest + "-1")]
    [InlineData("\"" + Digest + "-0\"")]
    [InlineData("\"" + Digest + "-01\"")]
    [InlineData("\"" + Digest + "-+1\"")]
    [InlineData("\"" + Digest + "--1\"")]
    [InlineData("\"" + Digest + "-1.0\"")]
    [InlineData("\"" + Digest + "-9223372036854775808\"")]
    public void Given_NoncanonicalTag_When_Parsed_Then_PreconditionIsRejected(string? tag)
    {
        Assert.False(ApplicationVersion.IsCanonical(tag));
        Assert.False(ApplicationVersion.Matches(tag, Identifier, 1));
    }

    /// <summary>Digest case, alphabet and width are fixed even when the rest of the strong-tag structure looks valid.</summary>
    [Fact]
    public void Given_InvalidDigest_When_Parsed_Then_PreconditionIsRejected()
    {
        Assert.False(ApplicationVersion.IsCanonical(FirstTag.ToUpperInvariant()));
        Assert.False(ApplicationVersion.IsCanonical(FirstTag.Replace('b', 'g')));
        Assert.False(ApplicationVersion.IsCanonical(ExtraDigestPrefix + Digest + RevisionSuffix));
        Assert.False(ApplicationVersion.IsCanonical(Quote + Digest[1..] + RevisionSuffix));
        Assert.False(ApplicationVersion.IsCanonical(FirstTag.Replace('-', '_')));
    }

    /// <summary>Invalid internal revision or resource input cannot mint a misleading canonical tag.</summary>
    [Fact]
    public void Given_InvalidAggregate_When_TagCreated_Then_NoTagIsReturned()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ApplicationVersion.Create(Identifier, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ApplicationVersion.Create(Identifier, -1));
        Assert.Throws<ArgumentException>(() => ApplicationVersion.Create(EmptyIdentifier, 1));
        Assert.Throws<ArgumentException>(() => ApplicationVersion.Create(new string(HighSurrogate, 1), 1));
        Assert.Throws<ArgumentException>(() => ApplicationVersion.Create(new string('x', OverlongIdentifier), 1));
    }
}
