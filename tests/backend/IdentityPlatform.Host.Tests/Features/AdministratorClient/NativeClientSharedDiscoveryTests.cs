using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Allows the shared issuer's exact optional UserInfo advertisement without authorizing a BFF request to it.</summary>
public sealed class NativeClientSharedDiscoveryTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    /// <summary>Only absent metadata or the canonical configured path is compatible; normalization never widens trust.</summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData("https://issuer.example/connect/userinfo", true)]
    [InlineData("https://other.example/connect/userinfo", false)]
    [InlineData("https://issuer.example/connect/UserInfo", false)]
    [InlineData("https://issuer.example/connect/userinfo/", false)]
    [InlineData("https://issuer.example/connect/userinfo?query=value", false)]
    [InlineData("https://issuer.example/connect/userinfo#fragment", false)]
    [InlineData("https://issuer.example:443/connect/userinfo", false)]
    [InlineData("http://issuer.example/connect/userinfo", false)]
    public void Given_SharedIssuerMetadata_When_UserInfoIsAdvertised_Then_OnlyExactOptionalDestinationIsAccepted(string? endpoint, bool accepted)
    {
        using var credentials = NativeClientProfileTests.Load(fixture);
        var profile = NativeClientProfile.Create(NativeClientProfileTests.Issuer, NativeClientProfileTests.Client,
            NativeClientProfileTests.Callback, NativeClientProfileTests.KeyId, credentials);
        var configuration = NativeClientProfileTests.Configuration(profile);
        configuration.UserInfoEndpoint = endpoint is null ? null : new Uri(endpoint);
        if (accepted)
        {
            NativeClientMetadata.Require(configuration, profile);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => NativeClientMetadata.Require(configuration, profile));
        }
    }
}
