using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Admin.Api.Features.Authentication;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ApiContracts = IdentityPlatform.Admin.Api.Features.ApiContracts.Setup;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Hosts a genuine independently scoped API with remote discovery and runtime-only Platform credentials.</summary>
internal sealed class ApplicationApiHost(IHost host, BrowserHttpsEndpoint endpoint) : IAsyncDisposable
{
    private const string TitleSetting = "ApiContracts:Metadata:Title";
    private const string DescriptionSetting = "ApiContracts:Metadata:Description";
    private const string Title = "Application API test";
    private const string Description = "Actual protected application list.";
    internal Uri Address => endpoint.Address;
    internal IServiceProvider Services => host.Services;

    /// <summary>Uses native HTTPS and real provider stores without issuer signing keys, cookies or authentication substitutes.</summary>
    internal static async Task<ApplicationApiHost> CreateAsync(JourneyScenario scenario, string provider, Uri issuer, string callback, ApplicationProxyProbe? probe = null, ApplicationMutationProbe? mutationProbe = null, Action<IServiceCollection>? configure = null)
    {
        var endpoint = new BrowserHttpsEndpoint();
        IHost? host = null;
        try
        {
            host = new HostBuilder().ConfigureWebHost(web =>
            {
                endpoint.Configure(web);
                web.ConfigureLogging(logging => logging.ClearProviders());
                web.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [TitleSetting] = Title,
                    [DescriptionSetting] = Description
                }));
                web.ConfigureServices((context, services) =>
                {
                    services.AddIdentityPersistence(DatabaseProviderParser.Parse(provider), scenario.Lease.RuntimeConnection);
                    services.AddSingleton<TimeProvider>(scenario.Clock);
                    ApiContracts.AddApiContracts(services, context.Configuration);
                    services.AddApplicationReads(issuer, callback);
                    configure?.Invoke(services);
                });
                web.Configure(app =>
                {
                    app.Use(endpoint.InvokeAsync);
                    if (probe is not null)
                    {
                        app.Use(probe.InvokeAsync);
                    }
                    if (mutationProbe is not null)
                    {
                        app.Use(mutationProbe.InvokeAsync);
                    }
                    app.UseRouting();
                    app.UseEndpoints(routes => routes.MapApplications());
                });
            }).Build();
            await host.StartAsync(TestContext.Current.CancellationToken);
            return new(host, endpoint);
        }
        catch
        {
            if (host is not null)
            {
                await new ApplicationApiHost(host, endpoint).DisposeAsync();
            }
            else
            {
                endpoint.Dispose();
            }
            throw;
        }
    }

    /// <summary>Joins network work before disposing scoped services and the bound listener.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await endpoint.StopAsync(host);
        }
        finally
        {
            try
            {
                if (host is IAsyncDisposable asynchronous)
                {
                    await asynchronous.DisposeAsync();
                }
                else
                {
                    host.Dispose();
                }
            }
            finally { endpoint.Dispose(); }
        }
    }
}
