using System.Net;
using IdentityPlatform.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IdentityPlatform.Host.Tests.Features.Health;

public sealed class HealthEndpointTests
{
    [Theory]
    [InlineData(HealthStatus.Healthy, HttpStatusCode.OK)]
    [InlineData(HealthStatus.Unhealthy, HttpStatusCode.ServiceUnavailable)]
    [InlineData(HealthStatus.Degraded, HttpStatusCode.ServiceUnavailable)]
    public async Task Dependency_status_affects_only_readiness_and_never_exposes_details(
        HealthStatus status, HttpStatusCode expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddPlatformServiceDefaults();
        builder.Services.AddHealthChecks().AddCheck("storage",
            () => new HealthCheckResult(status, "synthetic-secret-database-details"), tags: ["ready"]);
        await using var app = builder.Build();
        app.MapPlatformHealthEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        var live = await client.GetAsync("/alive?code=synthetic-secret", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        var ready = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        Assert.Equal(expected, ready.StatusCode);
        Assert.DoesNotContain("synthetic-secret", await ready.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(ready.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsync("/health", null, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/connect/token", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Slow_dependency_honors_health_check_deadline()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddPlatformServiceDefaults();
        builder.Services.AddHealthChecks().AddCheck<SlowDependency>("slow", tags: ["ready"],
            timeout: TimeSpan.FromMilliseconds(100));
        await using var app = builder.Build();
        app.MapPlatformHealthEndpoints();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(3));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health", deadline.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/alive", deadline.Token)).StatusCode);
    }

    private sealed class SlowDependency : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return HealthCheckResult.Healthy();
        }
    }
}
