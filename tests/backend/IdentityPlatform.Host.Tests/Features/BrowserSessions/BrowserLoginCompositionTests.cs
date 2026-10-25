using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Host.Tests.Features.AdministratorClient;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Requires explicit product/native identities and prevents automatic minimal-host authentication from escaping request ownership.</summary>
public sealed class BrowserLoginCompositionTests(NativeClientCredentialFixture fixture) : IClassFixture<NativeClientCredentialFixture>
{
    private const string ClientOrigin = "https://client.example";
    private const string OrdinaryFault = "ordinary";
    private const string IssuerFault = "issuer";
    private const string ClientFault = "client";
    private const string ForeignIdentity = "https://other.example/";
    /// <summary>An automatic minimal host is rejected before any product middleware is installed.</summary>
    [Fact]
    public async Task Given_AutomaticMinimalHost_When_BrowserLoginIsComposed_Then_ExplicitAdapterIsRequired()
    {
        await using var app = WebApplication.CreateBuilder().Build();
        var error = Assert.Throws<InvalidOperationException>(() => app.UseBrowserLogin());
        Assert.Equal(IdentityPlatform.Admin.Bff.Features.BrowserSessions.Setup.InvalidComposition, error.Message);
    }

    /// <summary>Incompatible fixed identity and callback policy denies before a route can mutate local state or start native transport.</summary>
    [Theory]
    [InlineData(OrdinaryFault)]
    [InlineData(IssuerFault)]
    [InlineData(ClientFault)]
    [InlineData("/bff/login")]
    [InlineData("/bff/login/resume")]
    [InlineData("/BFF/Login/")]
    [InlineData("/bff/session")]
    [InlineData("/BFF/Session/")]
    [InlineData("/bff/login/error")]
    [InlineData("/bff/logout")]
    [InlineData("/BFF/Logout/")]
    public void Given_IncompatibleNativeProfile_When_BrowserMiddlewareIsComposed_Then_StartupFailsSafely(string fault)
    {
        using var keys = NativeClientProfileTests.Load(fixture);
        var callback = fault.StartsWith('/') ? ClientOrigin + fault : NativeClientProfileTests.Callback;
        var profile = NativeClientProfile.Create(NativeClientProfileTests.Issuer, NativeClientProfileTests.Client, callback, NativeClientProfileTests.KeyId, keys);
        if (fault != OrdinaryFault)
        {
            profile = profile.ForBrowserSessions();
        }
        var services = new ServiceCollection().AddSingleton(profile).AddSingleton(TimeProvider.System)
            .AddSingleton(new BrowserSessionDestinations(fault == IssuerFault ? ForeignIdentity : NativeClientProfileTests.Issuer,
                fault == ClientFault ? ForeignIdentity : NativeClientProfileTests.Client));
        using var root = services.BuildServiceProvider();
        var error = Assert.Throws<InvalidOperationException>(() => new ApplicationBuilder(root).UseBrowserLogin());
        Assert.Equal(IdentityPlatform.Admin.Bff.Features.BrowserSessions.Setup.InvalidComposition, error.Message);
    }
    /// <summary>Encoded path aliases already fail native profile canonicalization before product route composition.</summary>
    [Fact]
    public void Given_EncodedProductPath_When_NativeProfileIsCreated_Then_CanonicalizationRejectsTheAlias()
    {
        const string EncodedCallback = "https://client.example/bff/login/%65rror";
        using var keys = NativeClientProfileTests.Load(fixture);
        Assert.Throws<IdentityPlatform.Admin.Bff.Features.Authentication.Credentials.ClientCredentialFailure>(() =>
            NativeClientProfile.Create(NativeClientProfileTests.Issuer, NativeClientProfileTests.Client, EncodedCallback, NativeClientProfileTests.KeyId, keys));
    }

}
