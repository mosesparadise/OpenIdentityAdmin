using System.Net;
using IdentityPlatform.AuthServer.Composition;
using IdentityPlatform.AuthServer.Features.Accounts.Browser;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.LocalDemo;

/// <summary>Exercises normal issuer activation and retained account/protocol wiring through its actual composition.</summary>
public sealed class LocalDemoIssuerTests(LocalDemoFixture fixture) : IClassFixture<LocalDemoFixture>
{
    private const string AlivePath = "/alive";
    private const string LoginPath = "/account/administrator/browser/login";
    private const string EnrollmentPath = "/account/administrator/enrollment";
    private const string ForeignIdentity = "https://foreign.example/";
    private const string Development = "Development";
    /// <summary>The explicit demo selects B3 accounts and issuer logout while keeping pilot and management routes dormant.</summary>
    [Fact]
    public async Task Given_ValidLocalIssuer_When_Started_Then_BrowserProfileAndLogoutAreActuallyComposed()
    {
        var root = fixture.CreateRoot();
        using (var startup = new StartupContext([]))
        {
            var builder = Create(fixture.IssuerSettings(root));
            await using var app = builder.AddConfigurations().ConfigureServices(startup).Build();
            app.ConfigureApplication(startup);
            await app.StartAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(app.Services.GetRequiredService<BrowserAccountProfile>());
            Assert.NotNull(app.Services.GetRequiredService<IssuerLogoutEnabled>());
            Assert.True(app.Services.GetRequiredService<AdministratorRegistrationPolicy>().IsBrowserSession);
            Assert.False(startup.PilotEnabled);
            using var client = app.GetTestClient();
            client.BaseAddress = new Uri(LocalDemoProfile.Issuer);
            using var alive = await client.GetAsync(AlivePath, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, alive.StatusCode);
            using var malformed = await client.GetAsync(LoginPath, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
            using var enrollment = await client.GetAsync(EnrollmentPath, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, enrollment.StatusCode);
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    }

    /// <summary>Conflicting issuer/callback identities deny before generating any demo material.</summary>
    [Theory]
    [InlineData("Identity:Issuer")]
    [InlineData("Identity:AdministratorProtocol:RedirectUri")]
    public void Given_ConflictingNativeIdentity_When_LocalIssuerIsComposed_Then_StartupFailsBeforePrivateWrites(string key)
    {
        var root = fixture.CreateRoot();
        var settings = fixture.IssuerSettings(root);
        settings[key] = ForeignIdentity;
        using var startup = new StartupContext([]);
        var builder = Create(settings);
        Assert.Equal(LocalDemoProfile.Failure, Assert.Throws<LocalDemoConfigurationException>(() => builder.AddConfigurations().ConfigureServices(startup)).Message);
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    }

    /// <summary>Loads the real issuer application parts and substitutes only configuration and HTTP transport.</summary>
    private static WebApplicationBuilder Create(Dictionary<string, string?> settings)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Development,
            ApplicationName = typeof(IssuerCredentials).Assembly.GetName().Name
        });
        builder.Configuration.AddInMemoryCollection(settings);
        builder.WebHost.UseTestServer();
        return builder;
    }
}
