using IdentityPlatform.Core.Features.Configuration;

namespace IdentityPlatform.Core.Tests.Features.Configuration;

public sealed class IssuerUriTests
{
    /// <summary>Verify valid HTTPS issuer paths and ports are preserved.</summary>
    [Theory]
    [InlineData("https://identity.example.test/")]
    [InlineData("https://localhost:7341/")]
    [InlineData("https://identity.example.test/issuer")]
    public void Given_AbsoluteHttpsIssuer_When_Parsed_Then_PreservesUri(string value)
    {
        Assert.Equal(new Uri(value), IssuerUri.Parse(value));
    }

    /// <summary>Verify unsafe or ambiguous issuers fail without disclosing input.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://localhost:7341/")]
    [InlineData("/issuer")]
    [InlineData("https://identity.example.test/?secret=SensitiveValue")]
    [InlineData("https://identity.example.test/#fragment")]
    [InlineData("https://user:SensitiveValue@identity.example.test/")]
    [InlineData(" https://identity.example.test/")]
    [InlineData("https://identity.example.test/ ")]
    [InlineData("file:///identity")]
    public void Given_UnsafeIssuer_When_Parsed_Then_RejectsWithoutDisclosingInput(string? value)
    {
        var exception = Assert.Throws<ArgumentException>(() => IssuerUri.Parse(value));

        Assert.Contains("Identity:Issuer", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SensitiveValue", exception.ToString(), StringComparison.Ordinal);
    }
}
