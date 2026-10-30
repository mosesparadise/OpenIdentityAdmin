using IdentityPlatform.Admin.Bff.Features.AdminUi;
using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Client;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Runs the explicit generic-host BFF over Kestrel with native network backchannels and unchanged TLS validation.</summary>
internal sealed class BrowserHttpsClient(IHost host, BrowserHttpsEndpoint endpoint) : IAsyncDisposable
{
    internal IServiceProvider Services => host.Services;

    /// <summary>Composes only Sessions credentials and existing native/browser services; no issuer bridge or injected authentication is installed.</summary>
    internal static async Task<BrowserHttpsClient> CreateAsync(BootstrapSessions database, TimeProvider clock, NativeClientProfile profile,
        NativeClientCredentials keys, SessionProtection protection, BrowserHttpsEndpoint endpoint, Action<IServiceCollection>? configure = null, bool workspace = false, Uri? api = null, bool mutations = false)
    {
        var builder = new HostBuilder().ConfigureWebHost(web =>
        {
            if (workspace)
            {
                web.UseEnvironment(Environments.Development);
            }
            endpoint.Configure(web);
            ApplicationClientSetup.Configure(web, api);
            web.ConfigureLogging(logging => logging.ClearProviders()).ConfigureServices(services =>
            {
                database.AddPersistence(services);
                services.AddSingleton(clock);
                services.AddAdministratorClient(profile, keys);
                services.AddBrowserLogin(new(profile.Issuer.AbsoluteUri, profile.ClientUri.AbsoluteUri), protection);
                services.Configure<OpenIddictClientOptions>(options => options.TimeProvider = clock);
                if (workspace)
                {
                    services.AddAdministratorUi();
                }
                configure?.Invoke(services);
            }).Configure(app =>
            {
                app.Use(endpoint.InvokeAsync);
                app.UseBrowserLogin();
                if (workspace)
                {
                    app.UseAdministratorUi();
                }
                ApplicationClientSetup.Use(app, api, mutations);
                app.Run(http =>
                {
                    http.Response.StatusCode = StatusCodes.Status404NotFound;
                    return Task.CompletedTask;
                });
            });
        });
        var host = builder.Build();
        var owner = new BrowserHttpsClient(host, endpoint);
        try
        {
            await host.StartAsync(TestContext.Current.CancellationToken);
            return owner;
        }
        catch { await owner.DisposeAsync(); throw; }
    }

    /// <summary>Joins BFF requests while issuer transport and all database/key owners are still alive.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await endpoint.StopAsync(host);
        }
        finally
        {
            if (host is IAsyncDisposable asyncHost)
            {
                await asyncHost.DisposeAsync();
            }
            else
            {
                host.Dispose();
            }
        }
    }
}
