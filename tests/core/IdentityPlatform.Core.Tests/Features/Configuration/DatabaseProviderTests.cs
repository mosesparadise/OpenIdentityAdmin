using IdentityPlatform.Core.Features.Configuration;

namespace IdentityPlatform.Core.Tests.Features.Configuration;

public sealed class DatabaseProviderTests
{
    [Theory]
    [InlineData("PostgreSql", DatabaseProvider.PostgreSql)]
    [InlineData("postgresql", DatabaseProvider.PostgreSql)]
    [InlineData(" SQLSERVER ", DatabaseProvider.SqlServer)]
    [InlineData("SqlServer", DatabaseProvider.SqlServer)]
    public void Accepts_only_named_supported_providers(string value, DatabaseProvider expected)
    {
        Assert.Equal(expected, DatabaseProviderParser.Parse(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Sqlite")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("PostgreSql,SqlServer")]
    [InlineData("Password=SensitiveValue")]
    public void Rejects_invalid_values_without_fallback_or_echoing_secrets(string? value)
    {
        var exception = Assert.Throws<ArgumentException>(() => DatabaseProviderParser.Parse(value));

        Assert.Contains("Database:Provider", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SensitiveValue", exception.ToString(), StringComparison.Ordinal);
    }
}
