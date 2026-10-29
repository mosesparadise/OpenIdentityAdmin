using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Host.Tests.Features.Applications;

/// <summary>Proves the external collection grammar cannot silently normalize identifiers or broaden query limits.</summary>
public sealed class ApplicationQueryTests
{
    private const int OverlongPrefix = 101;
    private const string Page = "page";
    private const string PageSize = "pageSize";
    private const string Search = "search";
    private const string FirstPage = "1";
    private const string SecondPage = "2";
    private const string DefaultSize = "25";
    private const string FirstSearch = "one";
    private const string SecondSearch = "two";
    private const string Unexpected = "more";
    private const string MaximumPage = "10000";
    private const string MaximumSize = "100";
    /// <summary>The absent query selects only the documented bounded first page.</summary>
    [Fact]
    public void Given_NoParameters_When_QueryParsed_Then_BoundedFirstPageIsSelected()
    {
        var result = ApplicationListQuery.Parse([]);
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Search);
        Assert.Equal(1, result.Page);
        Assert.Equal(25, result.PageSize);
    }

    /// <summary>Ambiguous, unknown and noncanonical values are rejected at the public grammar boundary.</summary>
    [Theory]
    [InlineData(Page, "0")]
    [InlineData(Page, "01")]
    [InlineData(Page, "+1")]
    [InlineData(Page, " 1")]
    [InlineData(Page, "1 ")]
    [InlineData(Page, "1.0")]
    [InlineData(Page, "10001")]
    [InlineData(Page, "99999999999999999999")]
    [InlineData(PageSize, "101")]
    [InlineData(PageSize, "0")]
    [InlineData(PageSize, "")]
    [InlineData("Page", FirstPage)]
    [InlineData("sort", "clientId")]
    [InlineData(Search, "bad\nvalue")]
    public void Given_NoncanonicalParameter_When_QueryParsed_Then_RequestIsRejected(string key, string value) =>
        Assert.Null(ApplicationListQuery.Parse([new(key, [value])]));

    /// <summary>Repeated fields and multivalue encodings cannot choose a first or last hidden alternative.</summary>
    [Fact]
    public void Given_AmbiguousParameters_When_QueryParsed_Then_NoAlternativeIsChosen()
    {
        Assert.Null(ApplicationListQuery.Parse([new(Page, [FirstPage]), new(Page, [SecondPage])]));
        Assert.Null(ApplicationListQuery.Parse([new(Search, [FirstSearch, SecondSearch])]));
        Assert.Null(ApplicationListQuery.Parse([new(Search, [])]));
        Assert.Null(ApplicationListQuery.Parse([new(Search, [new string('x', OverlongPrefix)])]));
        Assert.Null(ApplicationListQuery.Parse([new(Page, [FirstPage]), new(PageSize, [DefaultSize]), new(Search, [""]), new(Unexpected, [""])]));
    }

    /// <summary>Unicode, whitespace and wildcard-looking characters remain literal identifiers at their finite upper bounds.</summary>
    [Theory]
    [InlineData("%_[]\\")]
    [InlineData("a ")]
    [InlineData("Ä😀")]
    public void Given_LiteralPrefix_When_QueryParsed_Then_ValueAndMaximumPaginationArePreserved(string search)
    {
        var result = ApplicationListQuery.Parse([new(Search, [search]), new(Page, [MaximumPage]), new(PageSize, [MaximumSize])]);
        Assert.NotNull(result);
        Assert.Equal(search, result.Search);
        Assert.Equal(10000, result.Page);
        Assert.Equal(100, result.PageSize);
    }
}
