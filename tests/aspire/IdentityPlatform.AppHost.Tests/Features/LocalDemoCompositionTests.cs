using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Inspects the actual opt-in container graph and its material recipients for both registered providers.</summary>
[Trait("Category", "Harness")]
public sealed class LocalDemoCompositionTests
{
    private const string InitializationCommand = "initialize-administrator";
    private const string PlatformConnection = "ConnectionStrings__platform";
    private const string SessionsConnection = "ConnectionStrings__adminsessions";
    private const string ProvisionRoles = "LocalDevelopment__ProvisionRuntimeRoles";
    private const string IssuerVariable = "Identity__Issuer";
    private const string RedirectVariable = "Identity__AdministratorProtocol__RedirectUri";
    private const string PfxSuffix = "__Pfx";
    private const string PasswordSuffix = "__Password";
    private const string IssuerHost = "issuer.dev.localhost";
    private const string ClientHost = "bff.dev.localhost";
    private const string LoopbackAddress = "127.0.0.1";
    private const string RegistrationVariable = "LOCAL_DEMO_REGISTRATION";
    private const string UpgradeVariable = "LOCAL_DEMO_UPGRADE";
    private const string RevisionProperty = "expectedRevision";
    private const string PublicKeyProperty = "publicKey";
    private const string PublicKeyVariable = "Identity__AdministratorProtocol__PublicKey";
    private const string EnabledSetting = "LocalDemo:Enabled";
    private const string TrueValue = "true";
    private const string ProviderSetting = "Database:Provider";
    private const int ExpectedRsaBits = 3072;
    private const int PairedRoleFields = 2;
    private const int RoleTotal = 7;
    private const int PfxSuffixLength = 5;
    private const int IssuerPort = 17443;
    private const int ClientPort = 17444;
    private static readonly string[] PublicKeyFields = ["alg", "e", "kid", "kty", "n", "use"];
    private const string Development = "--environment=Development";
    private const string MaterialPrefix = "LocalDemoMaterial__";
    private const string IssuerResource = "auth";
    private const string ClientResource = "admin-bff";
    private const string InitializerResource = "initializer";
    private const string MigratorResource = "migrator";
    private static readonly string[] IssuerRoles = ["IssuerSigning", "IssuerEncryption", "IssuerWrapping"];
    private static readonly string[] ClientRoles = ["ClientStateSigning", "ClientStateEncryption", "ClientAssertion", "ClientWrapping"];

    /// <summary>Runtime resources receive only their own keys and store; initializer remains public-only and least-privilege.</summary>
    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public async Task Given_OptInDemo_When_GraphIsCreated_Then_CredentialsAndNativeIdentitiesRemainSeparated(string provider)
    {
        await using var builder = await CreateAsync(provider);
        var environments = await EnvironmentsAsync(builder);
        Assert.Empty(builder.Resources.OfType<ProjectResource>());
        RequireMaterial(environments[IssuerResource], IssuerRoles);
        RequireMaterial(environments[ClientResource], ClientRoles);
        foreach (var name in new[] { InitializerResource, MigratorResource })
        {
            Assert.DoesNotContain(environments[name].Keys, key => key.StartsWith(MaterialPrefix, StringComparison.Ordinal));
        }
        Assert.False(environments[InitializerResource].ContainsKey(SessionsConnection));
        Assert.False(environments[ClientResource].ContainsKey(PlatformConnection));
        Assert.False(environments[IssuerResource].ContainsKey(SessionsConnection));
        Assert.Equal("false", environments[InitializerResource][ProvisionRoles]);
        Assert.Equal(TrueValue, environments[MigratorResource][ProvisionRoles]);
        Assert.Equal("https://issuer.dev.localhost:17443/", environments[IssuerResource][IssuerVariable]);
        Assert.Equal("https://bff.dev.localhost:17444/bff/callback", environments[IssuerResource][RedirectVariable]);
        var fingerprints = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in new[] { IssuerResource, ClientResource })
        {
            await RequireDistinctKeys(environments[name], fingerprints);
        }
        Assert.Equal(RoleTotal, fingerprints.Count);
        RequireEndpoints(builder);
        RequirePublicRegistration(environments);
        var commands = builder.Resources.SelectMany(resource => resource.Annotations.OfType<ResourceCommandAnnotation>()
            .Where(command => command.Name == InitializationCommand).Select(command => (resource.Name, Command: command))).ToArray();
        Assert.Equal("initializer", Assert.Single(commands).Name);
        Assert.Empty(commands[0].Command.Arguments);
    }

    /// <summary>All private inputs are masked parameter objects, rather than raw environment literals.</summary>
    private static void RequireMaterial(Dictionary<string, object> environment, string[] roles)
    {
        var fields = environment.Where(pair => pair.Key.StartsWith(MaterialPrefix, StringComparison.Ordinal)).ToArray();
        Assert.Equal(roles.Length * PairedRoleFields, fields.Length);
        foreach (var role in roles)
        {
            Assert.True(Assert.IsType<ParameterResource>(environment[MaterialPrefix + role + PfxSuffix]).Secret);
            Assert.True(Assert.IsType<ParameterResource>(environment[MaterialPrefix + role + PasswordSuffix]).Secret);
        }
    }

    /// <summary>Imports actual generated keys without printing material and checks cross-host role independence.</summary>
    private static async Task RequireDistinctKeys(Dictionary<string, object> environment, HashSet<string> fingerprints)
    {
        foreach (var entry in environment.Where(pair => pair.Key.StartsWith(MaterialPrefix, StringComparison.Ordinal) && pair.Key.EndsWith(PfxSuffix, StringComparison.Ordinal)))
        {
            var encoded = await Assert.IsType<ParameterResource>(entry.Value).GetValueAsync(TestContext.Current.CancellationToken);
            var password = await Assert.IsType<ParameterResource>(environment[entry.Key[..^PfxSuffixLength] + PasswordSuffix]).GetValueAsync(TestContext.Current.CancellationToken);
            var bytes = Convert.FromBase64String(encoded!);
            try
            {
                using var certificate = X509CertificateLoader.LoadPkcs12(bytes, password, X509KeyStorageFlags.EphemeralKeySet);
                using var key = certificate.GetRSAPrivateKey();
                Assert.NotNull(key);
                Assert.Equal(ExpectedRsaBits, key.KeySize);
                Assert.True(fingerprints.Add(Convert.ToHexString(SHA256.HashData(certificate.GetPublicKey()))));
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
    }

    /// <summary>Requires exact same host/container HTTPS ports and canonical Docker aliases, with no explicit persistent mounts.</summary>
    private static void RequireEndpoints(IDistributedApplicationTestingBuilder builder)
    {
        foreach (var (name, alias, port) in new[] { (IssuerResource, IssuerHost, IssuerPort), (ClientResource, ClientHost, ClientPort) })
        {
            var resource = Assert.Single(builder.Resources.OfType<ContainerResource>(), value => value.Name == name);
            var endpoint = Assert.Single(resource.Annotations.OfType<EndpointAnnotation>());
            Assert.Equal(port, endpoint.Port);
            Assert.Equal(port, endpoint.TargetPort);
            Assert.False(endpoint.IsProxied);
            Assert.Equal(LoopbackAddress, endpoint.TargetHost);
            Assert.Contains(resource.Annotations.OfType<ContainerNetworkAliasAnnotation>(), value => value.Alias == alias);
            Assert.DoesNotContain(resource.Annotations.OfType<ContainerMountAnnotation>(), mount => mount.Type == ContainerMountType.Volume);
        }
    }

    /// <summary>The initializer receives only the public JWK used by the issuer, with a separate explicit revision-one upgrade.</summary>
    private static void RequirePublicRegistration(Dictionary<string, Dictionary<string, object>> environments)
    {
        using var registration = JsonDocument.Parse(Assert.IsType<string>(environments[InitializerResource][RegistrationVariable]));
        using var upgrade = JsonDocument.Parse(Assert.IsType<string>(environments[InitializerResource][UpgradeVariable]));
        Assert.Equal(1, upgrade.RootElement.GetProperty(RevisionProperty).GetInt32());
        var key = registration.RootElement.GetProperty(PublicKeyProperty);
        Assert.Equal(PublicKeyFields, key.EnumerateObject().Select(value => value.Name).Order(StringComparer.Ordinal));
        Assert.True(key.GetRawText() == Assert.IsType<string>(environments[IssuerResource][PublicKeyVariable]));
    }

    /// <summary>Injects explicit user configuration before the real AppHost composition executes.</summary>
    private static Task<IDistributedApplicationTestingBuilder> CreateAsync(string provider) =>
        DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>([Development], (_, host) =>
        {
            host.Configuration ??= new ConfigurationManager();
            host.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [EnabledSetting] = TrueValue, [ProviderSetting] = provider });
        }, TestContext.Current.CancellationToken);

    /// <summary>Evaluates native environment callbacks without launching containers or resolving connection secrets.</summary>
    private static async Task<Dictionary<string, Dictionary<string, object>>> EnvironmentsAsync(IDistributedApplicationTestingBuilder builder)
    {
        var result = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
        foreach (var container in builder.Resources.OfType<ContainerResource>())
        {
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            var context = new EnvironmentCallbackContext(builder.ExecutionContext, container, values, TestContext.Current.CancellationToken);
            foreach (var callback in container.Annotations.OfType<EnvironmentCallbackAnnotation>())
            {
                await callback.Callback(context);
            }
            result.Add(container.Name, values);
        }
        return result;
    }
}
