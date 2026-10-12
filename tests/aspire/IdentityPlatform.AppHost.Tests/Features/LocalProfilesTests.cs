using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IdentityPlatform.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.AppHost.Tests.Features;

public sealed class LocalProfilesTests
{
    private static readonly string[] HostNames = ["auth", "admin-api", "admin-bff", "worker"];
    [Theory]
    [InlineData("PostgreSql", "Development", Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", "Staging", Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", "Production", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", "Development", Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", "Staging", Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", "Production", Traits = ["Provider", "SqlServer"])]
    public async Task Selected_profile_migrates_and_serves_only_healthy_operational_endpoints(string provider, string environment)
    {
        using var credentials = new IssuerMaterialFixture();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        var cancellationToken = deadline.Token;
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            ["--environment=Development", $"--Database:Provider={provider}"], cancellationToken);
        foreach (var name in new[] { "admin-api", "admin-bff" })
        {
            builder.CreateResourceBuilder<ProjectResource>(name)
                .WithEnvironment("DOTNET_ENVIRONMENT", environment)
                .WithEnvironment("ASPNETCORE_ENVIRONMENT", environment)
                .WithEnvironment("Documentation__Enabled", "true");
        }
        var auth = builder.CreateResourceBuilder<ProjectResource>("auth");
        foreach (var setting in credentials.Settings)
        {
            var variable = setting.Key.Replace(":", "__");
            if (setting.Key.EndsWith(":Password", StringComparison.Ordinal))
            {
                var name = "issuer-" + setting.Key.Replace(":", "-").ToLowerInvariant();
                var secret = builder.AddParameter(name, () => setting.Value!, secret: true);
                auth.WithEnvironment(variable, secret);
            }
            else if (setting.Key == "Identity:Issuer")
            {
                auth.WithEnvironment(variable, auth.GetEndpoint("https"));
            }
            else
            {
                auth.WithEnvironment(variable, setting.Value!);
            }
        }
        await using var app = await builder.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);
        foreach (var name in HostNames)
        {
            await app.ResourceNotifications.WaitForResourceHealthyAsync(name, cancellationToken);
            using var client = new HttpClient { BaseAddress = app.GetEndpoint(name, "https") };
            Assert.Equal("https", client.BaseAddress!.Scheme);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/alive", cancellationToken)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", cancellationToken)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/connect/token", cancellationToken)).StatusCode);
            foreach (var path in new[]
            {
                "/api/v1/clients", "/api/v1/examples", "/bff/api/v1/examples",
                "/docs/scalar", "/docs/scalar/scalar.js", "/docs/scalar/scalar.aspnetcore.js",
                "/docs/swagger", "/docs/swagger/swagger-ui-bundle.js", "/docs/assets/swagger.js", "/docs/assets/theme.css",
                "/openapi/v1.json", "/openapi/bff-v1.json"
            })
            {
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path, cancellationToken)).StatusCode);
            }
        }
        Assert.NotEmpty(Directory.GetFiles(credentials.Ring, "key-*.xml"));
        using (var issuer = new HttpClient { BaseAddress = app.GetEndpoint("auth", "https") })
        {
            foreach (var path in new[] { "/.well-known/openid-configuration", "/.well-known/jwks", "/connect/jwks", "/connect/authorize", "/account/login" })
            {
                Assert.Equal(HttpStatusCode.NotFound, (await issuer.GetAsync(path, cancellationToken)).StatusCode);
            }
        }
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var endpoints = HostNames
            .Select(name => app.GetEndpoint(name, "https")).ToArray();
        Assert.Equal(endpoints.Length, endpoints.Distinct().Count());
        Assert.DoesNotContain(model.Resources.OfType<ContainerResource>(),
            resource => resource.Annotations.OfType<ContainerMountAnnotation>().Any());
        await app.Services.GetRequiredService<ResourceCommandService>().ExecuteCommandAsync(
            model.Resources.Single(resource => resource.Name == "database"), KnownResourceCommands.StopCommand, cancellationToken);
        await app.ResourceNotifications.WaitForResourceAsync("database", KnownResourceStates.Exited, cancellationToken);
        foreach (var name in HostNames)
        {
            using var client = new HttpClient { BaseAddress = app.GetEndpoint(name, "https") };
            using var healthDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var ready = await client.GetAsync("/health", healthDeadline.Token);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
            Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync(healthDeadline.Token));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/alive", healthDeadline.Token)).StatusCode);
        }
    }
}
