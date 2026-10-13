using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IdentityPlatform.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.AppHost.Tests.Features;

public sealed class LocalProfilesTests
{
    private const string HttpsEndpoint = "https";
    private const string AuthResource = "auth";
    private const string DatabaseResource = "database";
    private const string LivenessPath = "/alive";
    private const string ReadinessPath = "/health";
    private const string TokenPath = "/connect/token";
    private const string DevelopmentArgument = "--environment=Development";
    private const string DotnetEnvironment = "DOTNET_ENVIRONMENT";
    private const string AspnetEnvironment = "ASPNETCORE_ENVIRONMENT";
    private const string DocumentationEnabled = "Documentation__Enabled";
    private const string EnabledSetting = "true";
    private const string PasswordSuffix = ":Password";
    private const string IssuerSetting = "Identity:Issuer";
    private const string ConfigurationSeparator = ":";
    private const string EnvironmentSeparator = "__";
    private const string ParameterSeparator = "-";
    private const string IssuerParameterPrefix = "issuer-";
    private const string ProtectionKeyPattern = "key-*.xml";
    private const string ProviderArgumentPrefix = "--Database:Provider=";
    private static readonly TimeSpan ProfileDeadline = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan HealthDeadline = TimeSpan.FromSeconds(10);
    private static readonly string[] DocumentationHosts = ["admin-api", "admin-bff"];
    private static readonly string[] ClosedManagementPaths = new[]
            {
                "/api/v1/clients", "/api/v1/examples", "/bff/api/v1/examples",
                "/docs/scalar", "/docs/scalar/scalar.js", "/docs/scalar/scalar.aspnetcore.js",
                "/docs/swagger", "/docs/swagger/swagger-ui-bundle.js", "/docs/assets/swagger.js", "/docs/assets/theme.css",
                "/openapi/v1.json", "/openapi/bff-v1.json"
            };
    private static readonly string[] ClosedIssuerPaths = new[] { "/.well-known/openid-configuration", "/.well-known/jwks", "/connect/jwks", "/connect/authorize", "/account/login" };

    private static readonly string[] HostNames = [AuthResource, "admin-api", "admin-bff", "worker"];
    /// <summary>Requires isolated HTTPS hosts, closed application routes and dependency-aware health for each provider/profile.</summary>
    [Theory]
    [InlineData("PostgreSql", "Development", Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", "Staging", Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", "Production", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", "Development", Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", "Staging", Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", "Production", Traits = ["Provider", "SqlServer"])]
    public async Task Given_LocalProfile_When_StartedAndDatabaseStopped_Then_OnlyOperationalRoutesAndAccurateHealth(string provider, string environment)
    {
        using var credentials = new IssuerMaterialFixture();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(ProfileDeadline);
        var cancellationToken = deadline.Token;
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            [DevelopmentArgument, ProviderArgumentPrefix + provider], cancellationToken);
        ConfigureProfiles(builder, environment);
        ConfigureIssuer(builder, credentials);
        await using var app = await builder.BuildAsync(cancellationToken);
        await ProfileStartupEvidence.ObserveAsync(app, async () =>
        {
            await app.StartAsync(cancellationToken);
            await AssertHostsAsync(app, cancellationToken);
            Assert.NotEmpty(Directory.GetFiles(credentials.Ring, ProtectionKeyPattern));
            await AssertIssuerClosedAsync(app, cancellationToken);
            await AssertIsolationAndStopDatabaseAsync(app, cancellationToken);
            await AssertDegradedHealthAsync(app);
        });
    }

    /// <summary>Configures documentation requests against each requested API/BFF environment without enabling authentication.</summary>
    private static void ConfigureProfiles(IDistributedApplicationTestingBuilder builder, string environment)
    {
        foreach (var name in DocumentationHosts)
        {
            builder.CreateResourceBuilder<ProjectResource>(name)
                .WithEnvironment(DotnetEnvironment, environment)
                .WithEnvironment(AspnetEnvironment, environment)
                .WithEnvironment(DocumentationEnabled, EnabledSetting);
        }
    }

    /// <summary>Attaches disposable issuer material, registering passwords as secret parameters and the issuer as its HTTPS endpoint.</summary>
    private static void ConfigureIssuer(IDistributedApplicationTestingBuilder builder, IssuerMaterialFixture credentials)
    {
        var auth = builder.CreateResourceBuilder<ProjectResource>(AuthResource);
        foreach (var setting in credentials.Settings)
        {
            var variable = setting.Key.Replace(ConfigurationSeparator, EnvironmentSeparator);
            if (setting.Key.EndsWith(PasswordSuffix, StringComparison.Ordinal))
            {
                var name = IssuerParameterPrefix + setting.Key.Replace(ConfigurationSeparator, ParameterSeparator).ToLowerInvariant();
                var secret = builder.AddParameter(name, () => setting.Value!, secret: true);
                auth.WithEnvironment(variable, secret);
            }
            else if (setting.Key == IssuerSetting)
            {
                auth.WithEnvironment(variable, auth.GetEndpoint(HttpsEndpoint));
            }
            else
            {
                auth.WithEnvironment(variable, setting.Value!);
            }
        }
    }

    /// <summary>Requires each host to become healthy and expose only the operational routes within the original deadline.</summary>
    private static async Task AssertHostsAsync(DistributedApplication app, CancellationToken cancellationToken)
    {
        foreach (var name in HostNames)
        {
            await app.ResourceNotifications.WaitForResourceHealthyAsync(name, WaitBehavior.StopOnResourceUnavailable, cancellationToken);
            using var client = new HttpClient { BaseAddress = app.GetEndpoint(name, HttpsEndpoint) };
            Assert.Equal(Uri.UriSchemeHttps, client.BaseAddress!.Scheme);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(LivenessPath, cancellationToken)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(ReadinessPath, cancellationToken)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(TokenPath, cancellationToken)).StatusCode);
            foreach (var path in ClosedManagementPaths)
            {
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path, cancellationToken)).StatusCode);
            }
        }
    }

    /// <summary>Requires issuer discovery, protocol and account surfaces to stay closed in the default profile.</summary>
    private static async Task AssertIssuerClosedAsync(DistributedApplication app, CancellationToken cancellationToken)
    {
        using (var issuer = new HttpClient { BaseAddress = app.GetEndpoint(AuthResource, HttpsEndpoint) })
        {
            foreach (var path in ClosedIssuerPaths)
            {
                Assert.Equal(HttpStatusCode.NotFound, (await issuer.GetAsync(path, cancellationToken)).StatusCode);
            }
        }
    }

    /// <summary>Checks disposable storage and distinct HTTPS listeners, then stops the database within the case deadline.</summary>
    private static async Task AssertIsolationAndStopDatabaseAsync(DistributedApplication app, CancellationToken cancellationToken)
    {
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var endpoints = HostNames
            .Select(name => app.GetEndpoint(name, HttpsEndpoint)).ToArray();
        Assert.Equal(endpoints.Length, endpoints.Distinct().Count());
        Assert.DoesNotContain(model.Resources.OfType<ContainerResource>(),
            resource => resource.Annotations.OfType<ContainerMountAnnotation>().Any());
        await app.Services.GetRequiredService<ResourceCommandService>().ExecuteCommandAsync(
            model.Resources.Single(resource => resource.Name == DatabaseResource), KnownResourceCommands.StopCommand, cancellationToken);
        await app.ResourceNotifications.WaitForResourceAsync(DatabaseResource, KnownResourceStates.Exited, cancellationToken);
    }

    /// <summary>Requires readiness to fail while liveness survives database loss, retaining the ten-second per-host bound.</summary>
    private static async Task AssertDegradedHealthAsync(DistributedApplication app)
    {
        foreach (var name in HostNames)
        {
            using var client = new HttpClient { BaseAddress = app.GetEndpoint(name, HttpsEndpoint) };
            using var healthDeadline = new CancellationTokenSource(HealthDeadline);
            var ready = await client.GetAsync(ReadinessPath, healthDeadline.Token);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
            const string expectedUnhealthy = "Unhealthy";
            Assert.Equal(expectedUnhealthy, await ready.Content.ReadAsStringAsync(healthDeadline.Token));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(LivenessPath, healthDeadline.Token)).StatusCode);
        }
    }
}
