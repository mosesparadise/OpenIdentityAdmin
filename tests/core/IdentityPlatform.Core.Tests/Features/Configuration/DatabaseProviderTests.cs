using IdentityPlatform.Core.Features.Configuration;

namespace IdentityPlatform.Core.Tests.Features.Configuration;

public sealed class DatabaseProviderTests
{
    private static readonly string[] RegisteredNames = ["PostgreSql", "SqlServer"];
    /// <summary>Verify supported provider names retain case and whitespace normalization.</summary>
    [Theory]
    [InlineData("PostgreSql", "PostgreSql")]
    [InlineData("postgresql", "PostgreSql")]
    [InlineData(" SQLSERVER ", "SqlServer")]
    [InlineData("SqlServer", "SqlServer")]
    public void Given_SupportedProviderName_When_Parsed_Then_ReturnsExpectedProvider(string value, string expected)
    {
        Assert.Equal(expected, DatabaseProviderParser.Parse(value, RegisteredNames));
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
        var exception = Assert.Throws<ArgumentException>(() => DatabaseProviderParser.Parse(value, RegisteredNames));

        Assert.Contains("Database:Provider", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SensitiveValue", exception.ToString(), StringComparison.Ordinal);
    }
}
