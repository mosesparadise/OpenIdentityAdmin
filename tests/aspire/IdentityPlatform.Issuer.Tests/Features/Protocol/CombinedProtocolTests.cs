using System.Security.Cryptography;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.IdentityModel.Tokens;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

/// <summary>Preserves the real pilot bootstrap, cached authorization, account login and Basic exchange in the combined production host.</summary>
[Collection("issuer")]
public sealed class CombinedProtocolTests
{
    private const int KeyBits = 3072;
    private const string Callback = "https://administrator.example.test/callback";
    private const string KeyIdentifier = "combined-host-public-key";
    private const string KeyType = "RSA";
    private const string SignatureUse = "sig";
    private const string AuthResource = "auth";
    private const string BootstrapResource = "pilot-bootstrap";
    private const string AccountsSetting = "Identity__AdministratorAccounts__Enabled";
    private const string ProtocolSetting = "Identity__AdministratorProtocol__Enabled";
    private const string RedirectSetting = "Identity__AdministratorProtocol__RedirectUri";
    private const string ConfigurationPath = "/.well-known/openid-configuration";
    private const string ParPath = "/connect/par";
    private const string ParMetadata = "pushed_authorization_request_endpoint";
    private const string MethodsMetadata = "token_endpoint_auth_methods_supported";
    private const string ScopesMetadata = "scopes_supported";
    private const string BasicMethod = "client_secret_basic";
    private const string AssertionMethod = "private_key_jwt";
    private const string AdministratorScope = "identity.admin";
    private const string PublicKeySetting = "Identity__AdministratorProtocol__PublicKey";
    private static readonly Lazy<string> PublicKey = new(CreatePublicKey);

    /// <summary>Runs actual child-process composition and both real databases; no administrator registration or production BFF cookie is synthesized.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_CombinedProfiles_When_PilotCompletesNativeLoginAndExchange_Then_CachingAndLegacyPersistenceRemainValid(string provider)
    {
        await using var environment = await IssuerEnvironment.CreateAsync(provider, TestContext.Current.CancellationToken,
            configureBeforeBuild: (builder, _) =>
            {
                foreach (var name in new[] { AuthResource, BootstrapResource })
                {
                    builder.CreateResourceBuilder<ProjectResource>(name)
                        .WithEnvironment(AccountsSetting, bool.TrueString)
                        .WithEnvironment(ProtocolSetting, bool.TrueString)
                        .WithEnvironment(RedirectSetting, Callback)
                        .WithEnvironment(PublicKeySetting, PublicKey.Value);
                }
                return Task.CompletedTask;
            });
        using var browser = IssuerEnvironment.CreateBrowserClient();
        await RequireCombinedMetadataAsync(environment, browser);
        var flow = await environment.AuthorizeAsync(browser, exchange: true);
        Assert.True(flow.Validated);
        Assert.Empty(flow.Error);
        Assert.NotNull(flow.InteractionId);
        Assert.NotNull(flow.ExpectedSessionId);
    }

    /// <summary>Proves the actual running server enabled both profiles so an ignored configuration switch cannot leave a pilot-only control green.</summary>
    private static async Task RequireCombinedMetadataAsync(IssuerEnvironment environment, HttpClient browser)
    {
        using var response = await browser.GetAsync(new Uri(environment.Issuer, ConfigurationPath), TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(new Uri(environment.Issuer, ParPath).AbsoluteUri, document.RootElement.GetProperty(ParMetadata).GetString());
        Assert.Equal([BasicMethod, AssertionMethod], document.RootElement.GetProperty(MethodsMetadata)
            .EnumerateArray().Select(value => value.GetString()).Order(StringComparer.Ordinal));
        Assert.Contains(AdministratorScope, document.RootElement.GetProperty(ScopesMetadata).EnumerateArray().Select(value => value.GetString()));
    }

    /// <summary>Provides only synthetic public registration configuration; the private key is discarded and never used to bypass native authentication.</summary>
    private static string CreatePublicKey()
    {
        using var rsa = RSA.Create(KeyBits);
        var parameters = rsa.ExportParameters(false);
        return JsonSerializer.Serialize(new
        {
            kty = KeyType,
            kid = KeyIdentifier,
            alg = SecurityAlgorithms.RsaSha256,
            use = SignatureUse,
            n = Base64UrlEncoder.Encode(parameters.Modulus),
            e = Base64UrlEncoder.Encode(parameters.Exponent)
        });
    }
}
