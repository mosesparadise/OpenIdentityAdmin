using IdentityPlatform.Core.Features.Configuration;

namespace IdentityPlatform.Core.Tests.Features.Configuration;

public sealed class DatabaseProviderTests
{
    /// <summary>Verify supported provider names retain case and whitespace normalization.</summary>
    [Theory]
    [InlineData("PostgreSql", DatabaseProvider.PostgreSql)]
    [InlineData("postgresql", DatabaseProvider.PostgreSql)]
    [InlineData(" SQLSERVER ", DatabaseProvider.SqlServer)]
    [InlineData("SqlServer", DatabaseProvider.SqlServer)]
    public void Given_SupportedProviderName_When_Parsed_Then_ReturnsExpectedProvider(string value, DatabaseProvider expected)
    {
        Assert.Equal(expected, DatabaseProviderParser.Parse(value));
    }

    /// <summary>Verify invalid providers fail without fallback or disclosure of supplied secrets.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Sqlite")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("PostgreSql,SqlServer")]
    [InlineData("Password=SensitiveValue")]
    public void Given_InvalidProvider_When_Parsed_Then_RejectsWithoutDisclosingInput(string? value)
    {
        var exception = Assert.Throws<ArgumentException>(() => DatabaseProviderParser.Parse(value));

        Assert.Contains("Database:Provider", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SensitiveValue", exception.ToString(), StringComparison.Ordinal);
    }
}
