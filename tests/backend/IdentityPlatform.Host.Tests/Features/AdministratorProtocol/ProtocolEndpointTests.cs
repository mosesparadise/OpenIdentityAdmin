using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.AdministratorProtocol;

/// <summary>Exercises actual pinned native URI inference used for boundary routing, avoiding a second lexical URL matcher.</summary>
public sealed class ProtocolEndpointTests
{
    private const string ConfigurationPath = "/.well-known/openid-configuration";
    private const string KeysPath = "/connect/jwks";
    private const string Scheme = "https";

    /// <summary>Includes native case/trailing-slash, PathBase, absolute-host/port and nonmatching route controls.</summary>
    [Theory]
    [InlineData("/connect/authorize", "", "issuer.example.test", "/connect/authorize", OpenIddictServerEndpointType.Authorization)]
    [InlineData("/CONNECT/AUTHORIZE/", "", "issuer.example.test", "/connect/authorize", OpenIddictServerEndpointType.Authorization)]
    [InlineData("/connect/authorize", "/identity", "issuer.example.test", "/connect/authorize", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/authorize", "/identity/", "issuer.example.test", "connect/authorize", OpenIddictServerEndpointType.Authorization)]
    [InlineData("/connect/authorize//", "", "issuer.example.test", "/connect/authorize", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/token", "", "issuer.example.test", "/connect/authorize", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/authorize", "", "issuer.example.test:8443", "https://issuer.example.test:8443/connect/authorize", OpenIddictServerEndpointType.Authorization)]
    [InlineData("/connect/authorize", "", "issuer.example.test", "https://issuer.example.test:8443/connect/authorize", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/connect/authorize", "", "other.example.test", "https://issuer.example.test/connect/authorize", OpenIddictServerEndpointType.Unknown)]
    [InlineData("/.WELL-KNOWN/OPENID-CONFIGURATION/", "", "issuer.example.test", "/connect/authorize", OpenIddictServerEndpointType.Configuration)]
    [InlineData("/CONNECT/JWKS/", "", "issuer.example.test", "/connect/authorize", OpenIddictServerEndpointType.JsonWebKeySet)]
    public async Task Given_ConfiguredNativeUris_When_MiddlewareClassifies_Then_NativeEquivalentUrlsShareOwnership(string path, string pathBase, string host,
        string configured, OpenIddictServerEndpointType expected)
    {
        var services = new ServiceCollection();
        services.AddOptions<OpenIddictServerOptions>().Configure(options =>
        {
            options.AuthorizationEndpointUris.Add(new Uri(configured, UriKind.RelativeOrAbsolute));
            options.ConfigurationEndpointUris.Add(new Uri(ConfigurationPath, UriKind.Relative));
            options.JsonWebKeySetEndpointUris.Add(new Uri(KeysPath, UriKind.Relative));
        });
        await using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider, RequestAborted = TestContext.Current.CancellationToken };
        http.Request.Scheme = Scheme;
        http.Request.Host = new HostString(host);
        http.Request.Path = path;
        http.Request.PathBase = pathBase;
        Assert.Equal(expected, await ProtocolEndpoint.ResolveAsync(http));
    }
}
