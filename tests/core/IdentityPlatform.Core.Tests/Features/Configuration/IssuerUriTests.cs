using IdentityPlatform.Core.Features.Configuration;

namespace IdentityPlatform.Core.Tests.Features.Configuration;

public sealed class IssuerUriTests
{
    [Theory]
    [InlineData("https://identity.example.test/")]
    [InlineData("https://localhost:7341/")]
    [InlineData("https://identity.example.test/issuer")]
    public void Accepts_absolute_https_issuer(string value)
    {
        Assert.Equal(new Uri(value), IssuerUri.Parse(value));
    }

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
    public void Rejects_unsafe_or_ambiguous_issuer_without_echoing_input(string? value)
    {
        var exception = Assert.Throws<ArgumentException>(() => IssuerUri.Parse(value));

        Assert.Contains("Identity:Issuer", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SensitiveValue", exception.ToString(), StringComparison.Ordinal);
    }
}
