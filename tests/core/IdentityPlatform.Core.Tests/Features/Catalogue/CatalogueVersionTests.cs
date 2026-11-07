using IdentityPlatform.Core.Features.Catalogue;

namespace IdentityPlatform.Core.Tests.Features.Catalogue;

/// <summary>Proves strong preconditions cannot be transferred across resource kind, identity, case or revision.</summary>
public sealed class CatalogueVersionTests
{
    private const int CurrentRevision = 7;
    private const int NextRevision = 8;
    private const int IdentifierOverflow = 101;

    private const string ScopeKind = "scope";
    private const string AudienceKind = "audience";
    private const string ResourceId = "native-one";
    private const string OtherResourceId = "native-two";
    private const string CaseResourceId = "Native-one";
    private const string KindAlias = "Scope";
    private const string ShortId = "id";
    private const string FramingId = "id\0scope";
    private const string Wildcard = "*";

    private const string Expected = "\"fde3f6ad896f09574a2c2a13c11c08882eeada45127b2642f8423b45b59af4d6\"";

    /// <summary>Checks a separately computed framing vector and exact current storage bindings.</summary>
    [Fact]
    public void Given_ResourceVersion_When_ComparedAcrossBindings_Then_OnlyExactKindIdRevisionMatches()
    {
        Assert.Equal(Expected, CatalogueVersion.Create(ScopeKind, ResourceId, CurrentRevision));
        Assert.True(CatalogueVersion.Matches(Expected, ScopeKind, ResourceId, CurrentRevision));
        Assert.False(CatalogueVersion.Matches(Expected, AudienceKind, ResourceId, CurrentRevision));
        Assert.False(CatalogueVersion.Matches(Expected, ScopeKind, OtherResourceId, CurrentRevision));
        Assert.False(CatalogueVersion.Matches(Expected, ScopeKind, CaseResourceId, CurrentRevision));
        Assert.False(CatalogueVersion.Matches(Expected, ScopeKind, ResourceId, NextRevision));
        Assert.NotEqual(Expected, CatalogueVersion.Create(ScopeKind, ResourceId, long.MaxValue));
    }

    /// <summary>Rejects conditional-header aliases instead of accepting wildcard or weak preconditions.</summary>
    [Theory]
    [InlineData("W/", "")]
    [InlineData(" ", "")]
    [InlineData("", " ")]
    [InlineData("", ",*")]
    [InlineData(Wildcard, "")]
    public void Given_StrongVersion_When_HeaderAliasAdded_Then_PreconditionIsRejected(string before, string after) =>
        Assert.False(CatalogueVersion.IsCanonical(before + Expected + after));

    /// <summary>Invalid storage identities and revisions cannot produce apparently valid preconditions.</summary>
    [Fact]
    public void Given_InvalidStoredIdentity_When_VersionCreated_Then_ItFailsClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CatalogueVersion.Create(ScopeKind, ShortId, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => CatalogueVersion.Create(ScopeKind, ShortId, -1));
        Assert.Throws<ArgumentException>(() => CatalogueVersion.Create(KindAlias, ShortId, 1));
        Assert.Throws<ArgumentException>(() => CatalogueVersion.Create(ScopeKind, FramingId, 1));
        Assert.Throws<ArgumentException>(() => CatalogueVersion.Create(ScopeKind, new string('a', IdentifierOverflow), 1));
        Assert.False(CatalogueVersion.IsCanonical(Expected.ToUpperInvariant()));
        Assert.False(CatalogueVersion.IsCanonical(null));
        Assert.False(CatalogueVersion.IsCanonical(Wildcard));
        Assert.False(CatalogueVersion.IsCanonical(Expected[..^1]));
    }
}
