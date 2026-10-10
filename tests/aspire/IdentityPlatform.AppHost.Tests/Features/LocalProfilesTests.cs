using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.AppHost.Tests.Features;

public sealed class LocalProfilesTests
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Selected_profile_migrates_and_serves_only_healthy_operational_endpoints(string provider)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(3));
        var cancellationToken = deadline.Token;
        await using var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            ["--environment=Development", $"--Database:Provider={provider}"], cancellationToken);
        await using var app = await builder.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);
        foreach (var name in new[] { "auth", "admin-api", "admin-bff", "worker" })
        {
            await app.ResourceNotifications.WaitForResourceHealthyAsync(name, cancellationToken);
            using var client = new HttpClient { BaseAddress = app.GetEndpoint(name, "https") };
            Assert.Equal("https", client.BaseAddress!.Scheme);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/alive", cancellationToken)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health", cancellationToken)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/connect/token", cancellationToken)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/clients", cancellationToken)).StatusCode);
        }
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var endpoints = new[] { "auth", "admin-api", "admin-bff", "worker" }
            .Select(name => app.GetEndpoint(name, "https")).ToArray();
        Assert.Equal(endpoints.Length, endpoints.Distinct().Count());
        Assert.DoesNotContain(model.Resources.OfType<ContainerResource>(),
            resource => resource.Annotations.OfType<ContainerMountAnnotation>().Any());
        await app.Services.GetRequiredService<ResourceCommandService>().ExecuteCommandAsync(
            model.Resources.Single(resource => resource.Name == "database"), KnownResourceCommands.StopCommand, cancellationToken);
        await app.ResourceNotifications.WaitForResourceAsync("database", KnownResourceStates.Exited, cancellationToken);
        foreach (var name in new[] { "auth", "admin-api", "admin-bff", "worker" })
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
