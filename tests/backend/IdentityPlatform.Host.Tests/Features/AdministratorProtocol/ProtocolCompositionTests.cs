using System.Net;
using System.Text.Json;
using IdentityPlatform.Host.Tests.Features.Accounts;
using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.AdministratorProtocol;

/// <summary>Exercises actual shared production composition and native discovery in administrator-only and combined modes.</summary>
public sealed class ProtocolCompositionTests(AdministratorRegistrationFixture fixture) : IClassFixture<AdministratorRegistrationFixture>
{
    private const string Callback = "https://administrator.example.test/callback";
    private const string ConfigurationPath = "/.well-known/openid-configuration";
    private const string LoginPath = "/account/login";
    private const string AdministratorLoginPath = "/account/administrator/login";
    private const string EnabledSetting = "Identity:AdministratorProtocol:Enabled";
    private const string RedirectSetting = "Identity:AdministratorProtocol:RedirectUri";
    private const string PublicKeySetting = "Identity:AdministratorProtocol:PublicKey";
    private const string ParMetadata = "pushed_authorization_request_endpoint";
    private const string ParPath = "/connect/par";
    private const string KeysPath = "/connect/jwks";
    private const string UnknownPath = "/unknown";

    /// <summary>Checks actual native option union, conditional caching, account page manager selection and database-free discovery/JWKS.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_ExplicitAdministratorProtocol_When_HostStarts_Then_OnlyEnabledProfilesAreAdvertised(bool pilot)
    {
        await using var host = await AccountHost.StartAsync(pilot, true, configureConfiguration: Configure);
        var options = host.Services.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue;
        Assert.Equal(pilot ? ["client_secret_basic", "private_key_jwt"] : ["private_key_jwt"], options.ClientAuthenticationMethods.Order(StringComparer.Ordinal));
        Assert.Equal(pilot ? ["identity.admin", "openid", "pilot_api"] : ["identity.admin", "openid"], options.Scopes.Order(StringComparer.Ordinal));
        Assert.Equal(pilot ? ["login", "none"] : ["none"], options.PromptValues.Order(StringComparer.Ordinal));
        Assert.Equal(pilot, options.EnableAuthorizationRequestCaching);
        Assert.Equal([new Uri(ParPath, UriKind.Relative)], options.PushedAuthorizationEndpointUris);
        using var discovery = await host.Client.GetAsync(ConfigurationPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, discovery.StatusCode);
        using var document = JsonDocument.Parse(await discovery.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("https://issuer.example.test/connect/par", document.RootElement.GetProperty(ParMetadata).GetString());
        using var keys = await host.Client.GetAsync(KeysPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, keys.StatusCode);
        using var login = await host.Client.GetAsync(LoginPath, TestContext.Current.CancellationToken);
        Assert.Equal(pilot ? HttpStatusCode.BadRequest : HttpStatusCode.NotFound, login.StatusCode);
        using var administrator = await host.Client.GetAsync(AdministratorLoginPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, administrator.StatusCode);
        using var unknown = await host.Client.GetAsync(UnknownPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    /// <summary>Uses the real production switch and public-only registration settings; it never initializes native rows.</summary>
    private void Configure(IConfiguration configuration)
    {
        configuration[EnabledSetting] = bool.TrueString;
        configuration[RedirectSetting] = Callback;
        configuration[PublicKeySetting] = fixture.Json;
    }
}
