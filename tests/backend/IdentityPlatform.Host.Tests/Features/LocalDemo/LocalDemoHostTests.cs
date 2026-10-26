using System.Net;
using IdentityPlatform.Admin.Bff.Composition;
using IdentityPlatform.Admin.Bff.Features.LocalDemo;
using IdentityPlatform.Core.Features.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace IdentityPlatform.Host.Tests.Features.LocalDemo;

/// <summary>Exercises the BFF's actual generic startup rather than a duplicate fixture pipeline.</summary>
public sealed class LocalDemoHostTests(LocalDemoFixture fixture) : IClassFixture<LocalDemoFixture>
{
    private const string EnabledSetting = "LocalDemo:Enabled";
    private const string AlivePath = "/alive";
    private const string UnknownPath = "/unknown";
    private const string ApiPath = "/bff/api/v1/users";
    private const string ScalarPath = "/scalar";
    private const string SwaggerPath = "/swagger";
    private const string LoginPath = "/bff/login";
    private const string SessionPath = "/bff/session";
    private const string InvalidCallback = "/bff/callback?unexpected=synthetic";
    private const string SetCookieHeader = "Set-Cookie";
    private const int ClientErrorThreshold = 400;
    /// <summary>Live operational and unknown routes cannot trigger unowned native authentication, even when the demo is enabled.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_RuntimeComposition_When_OperationalAndUnknownRoutesRun_Then_NativeClientDoesNotEscapeOwnership(bool enabled)
    {
        var root = fixture.CreateRoot();
        var settings = fixture.BffSettings(root);
        settings[EnabledSetting] = enabled.ToString();
        using (var startup = new StartupContext())
        using (var host = Create(startup, settings).Build())
        {
            await host.StartAsync(TestContext.Current.CancellationToken);
            using var client = host.GetTestClient();
            client.BaseAddress = new Uri(LocalDemoProfile.Client);
            using var alive = await client.GetAsync(AlivePath, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
            foreach (var path in new[] { UnknownPath, ApiPath, ScalarPath, SwaggerPath })
            {
                using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
            using var login = await client.GetAsync(LoginPath, TestContext.Current.CancellationToken);
            // Login creates durable browser coordination; unavailable storage must fail closed.
            Assert.Equal(enabled ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NotFound, login.StatusCode);
            using var session = await client.GetAsync(SessionPath, TestContext.Current.CancellationToken);
            Assert.Equal(enabled ? HttpStatusCode.OK : HttpStatusCode.NotFound, session.StatusCode);
            await host.StopAsync(TestContext.Current.CancellationToken);
        }
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    }

    /// <summary>Explicit opt-in cannot activate under a production environment before a listener exists.</summary>
    [Fact]
    public void Given_ProductionEnvironment_When_DemoIsEnabled_Then_StartupRejectsBeforeMaterialWrites()
    {
        var root = fixture.CreateRoot();
        using var startup = new StartupContext();
        var error = Assert.Throws<LocalDemoConfigurationException>(() => Create(startup, fixture.BffSettings(root)).UseEnvironment(Environments.Production).Build());
        Assert.Equal(LocalDemoProfile.Failure, error.Message);
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    }

    /// <summary>A rejected callback cannot publish a browser session or redirect to an untrusted destination.</summary>
    [Fact]
    public async Task Given_InvalidCallback_When_NormalRuntimeHandlesIt_Then_NoSessionOrRedirectIsPublished()
    {
        using var startup = new StartupContext();
        using var host = Create(startup, fixture.BffSettings(fixture.CreateRoot())).Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        using var client = host.GetTestClient();
        client.BaseAddress = new Uri(LocalDemoProfile.Client);
        using var response = await client.GetAsync(InvalidCallback, TestContext.Current.CancellationToken);
        Assert.True((int)response.StatusCode >= ClientErrorThreshold);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains(SetCookieHeader));
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Uses the actual Program host builder with only configuration and an in-memory HTTP transport replaced.</summary>
    private static IHostBuilder Create(StartupContext startup, Dictionary<string, string?> settings) => HostSetup.Create([], startup)
        .UseEnvironment(Environments.Development).ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(settings))
        .ConfigureWebHost(web => web.UseTestServer());
}
