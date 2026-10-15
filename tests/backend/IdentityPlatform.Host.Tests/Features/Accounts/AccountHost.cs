using IdentityPlatform.AuthServer.Composition;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.Accounts;

/// <summary>Owns a real production composition with TestServer transport and disposable issuer protection.</summary>
internal sealed class AccountHost : IAsyncDisposable
{
    internal const string Probe = "/account/administrator/probe";
    internal const string AdministratorSetting = "Identity:AdministratorAccounts:Enabled";
    private const string PilotSetting = "Pilot:Enabled";
    private const string ProviderSetting = "Database:Provider";
    private const string ConnectionSetting = "ConnectionStrings:platform";
    private const string Provider = "PostgreSql";
    private const string Connection = "Host=localhost;Port=1;Database=synthetic;Username=synthetic;Password=synthetic";
    private const string Development = "Development";
    private const string Production = "Production";
    private readonly IssuerMaterialFixture material = new();
    private readonly StartupContext startup = new([]);
    private WebApplication? app;
    internal HttpClient Client { get; private set; } = null!;
    internal IServiceProvider Services => app!.Services;

    /// <summary>Starts the exact host setup with independent feature switches; unwinds credentials if startup fails.</summary>
    internal static async Task<AccountHost> StartAsync(bool pilot, bool administrator, Action<WebApplication>? configure = null, Action<IServiceCollection>? configureServices = null)
    {
        var host = new AccountHost();
        try
        {
            var builder = host.Builder(pilot, administrator);
            builder.AddConfigurations().ConfigureServices(host.startup);
            configureServices?.Invoke(builder.Services);
            host.app = builder.Build();
            host.app.ConfigureApplication(host.startup);
            configure?.Invoke(host.app);
            await host.app.StartAsync(TestContext.Current.CancellationToken);
            host.Client = host.app.GetTestClient();
            host.Client.BaseAddress = host.startup.Credentials!.Issuer;
            return host;
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }
    }

    /// <summary>Loads disposable settings after default providers; admin-only exercises Production rather than enabling the pilot.</summary>
    private WebApplicationBuilder Builder(bool pilot, bool administrator)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(IssuerCredentials).Assembly.GetName().Name,
            EnvironmentName = pilot ? Development : Production
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(material.Settings);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ProviderSetting] = Provider,
            [ConnectionSetting] = Connection,
            [PilotSetting] = pilot.ToString(),
            [AdministratorSetting] = administrator.ToString()
        });
        return builder;
    }

    /// <summary>Disposes HTTP/host before externally owned credentials, including failed-start paths.</summary>
    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        try
        {
            if (app is not null)
            {
                await app.DisposeAsync();
            }
        }
        finally
        {
            startup.Dispose();
            material.Dispose();
        }
    }
}
