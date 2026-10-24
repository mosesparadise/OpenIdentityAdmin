using System.Net;
using IdentityPlatform.Admin.Bff.Features.Authentication.Credentials;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using OpenIddict.Client;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises the dormant explicit middleware host using real native HTTP and Sessions services without claiming trusted TLS.</summary>
internal sealed class BrowserLoginHost(IHost host, NativeClientIssuerBridge bridge, Uri client) : IAsyncDisposable
{
    private const string HealthPath = "/health";
    internal IServiceProvider Services => host.Services;
    internal NativeClientIssuerBridge Bridge => bridge;

    /// <summary>Builds a separate native client root and places terminal login before unrelated routes without global authentication dispatch.</summary>
    internal static async Task<BrowserLoginHost> CreateAsync(BootstrapSessions database, AdministratorProtocolScenario issuer, IServiceProvider protocol,
        NativeClientProfile profile, NativeClientCredentials keys, SessionProtection protection, Action<IServiceCollection>? configure = null)
    {
        var bridge = new NativeClientIssuerBridge(issuer, protocol);
        var builder = new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureLogging(logging => logging.ClearProviders()).ConfigureServices(services =>
        {
            database.AddPersistence(services);
            services.AddSingleton<TimeProvider>(issuer.Account.Journey.Clock);
            services.AddAdministratorClient(profile, keys);
            services.AddBrowserLogin(new(profile.Issuer.AbsoluteUri, profile.ClientUri.AbsoluteUri), protection);
            services.Configure<OpenIddictClientOptions>(options => options.TimeProvider = issuer.Account.Journey.Clock);
            services.ConfigureAll<HttpClientFactoryOptions>(options =>
            {
                options.HandlerLifetime = Timeout.InfiniteTimeSpan;
                options.HttpMessageHandlerBuilderActions.Add(builder => builder.AdditionalHandlers.Add(bridge));
            });
            configure?.Invoke(services);
        }).Configure(app =>
        {
            app.UseBrowserLogin();
            app.Run(http =>
            {
                http.Response.StatusCode = http.Request.Path == HealthPath ? StatusCodes.Status200OK : StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            });
        }));
        IHost? built = null;
        try
        {
            built = builder.Build();
            await built.StartAsync(TestContext.Current.CancellationToken);
            return new(built, bridge, profile.ClientUri);
        }
        catch (Exception error)
        {
            try
            {
                built?.Dispose();
            }
            catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
            finally { bridge.Dispose(); }
            throw;
        }
    }

    /// <summary>Uses a scoped browser cookie container over TestServer with no redirect or HTTP retry behavior.</summary>
    internal HttpClient Browser(CookieContainer cookies) => new(new BrowserIssuerCookies(cookies, host.GetTestServer().CreateHandler())) { BaseAddress = client };

    /// <summary>Disposes the HTTP/native service root before any caller-owned database or protection keys.</summary>
    public ValueTask DisposeAsync()
    {
        try
        {
            host.Dispose();
        }
        finally { bridge.Dispose(); }
        return ValueTask.CompletedTask;
    }
}
