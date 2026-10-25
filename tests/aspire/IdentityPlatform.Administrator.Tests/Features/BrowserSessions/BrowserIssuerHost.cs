using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.AuthServer.Features.Accounts;
using IdentityPlatform.AuthServer.Features.Accounts.Browser;
using IdentityPlatform.AuthServer.Features.Accounts.Maintenance;
using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Issuer.DataProtection;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Hosts real Razor/native accounts over a real provider with in-process HTTP transport; it is not TLS or browser-engine evidence.</summary>
internal sealed class BrowserIssuerHost(WebApplication app, IssuerCredentials credentials, Uri issuer) : IAsyncDisposable
{
    internal const string Client = "https://administrator.example.test/";
    internal IServiceProvider Services => app.Services;
    internal Uri Issuer => issuer;

    /// <summary>Explicitly enables only the reviewed browser account adapters; production calls retain their disabled default.</summary>
    internal static Task<BrowserIssuerHost> CreateAsync(JourneyScenario scenario, string provider, params IInterceptor[] faults) =>
        CreateCoreAsync(scenario, provider, false, faults);

    /// <summary>Enables a same-key pilot only inside this isolated account fixture for genuine scheme-isolation controls.</summary>
    internal static Task<BrowserIssuerHost> CreatePilotAsync(JourneyScenario scenario, string provider) =>
        CreateCoreAsync(scenario, provider, true, []);

    /// <summary>Composes one explicit account host and preserves cleanup ownership under failed startup.</summary>
    private static async Task<BrowserIssuerHost> CreateCoreAsync(JourneyScenario scenario, string provider, bool pilot, IInterceptor[] faults)
    {
        var builder = CreateBuilder(scenario, provider, pilot, faults, out var credentials);
        WebApplication? app = null;
        try
        {
            app = builder.Build();
            app.Services.VerifyIssuerProtection();
            app.UseRouting();
            app.UseRequestTimeouts();
            app.UseAccountProtection(pilot);
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapAccounts();
            await app.StartAsync(TestContext.Current.CancellationToken);
            if (pilot)
            {
                Assert.Equal(0, await app.InitializePilotAsync());
            }
            return new(app, credentials, credentials.Issuer);
        }
        catch
        {
            if (app is not null)
            {
                await app.DisposeAsync();
            }
            credentials.Dispose();
            throw;
        }
    }

    /// <summary>Shares real provider, key and account setup while limiting the optional pilot to explicit disposable tests.</summary>
    private static WebApplicationBuilder CreateBuilder(JourneyScenario scenario, string provider, bool pilot, IInterceptor[] faults, out IssuerCredentials credentials)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(IssuerCredentials).Assembly.GetName().Name,
            EnvironmentName = Environments.Production
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(scenario.IssuerSettings);
        builder.Services.AddIdentityPersistence(DatabaseProviderParser.Parse(provider), scenario.Lease.RuntimeConnection);
        builder.Services.AddDbContext<PlatformDbContext>(options => options.AddInterceptors(faults));
        credentials = builder.Services.AddIssuerProtection(builder.Configuration)!;
        if (pilot)
        {
            BrowserPilotCookie.Configure(builder, scenario);
            builder.Services.AddPilotServices().AddProtocol(credentials);
        }
        builder.Services.AddSingleton<TimeProvider>(scenario.Clock);
        builder.Services.AddAdministratorFactors(credentials.Issuer).AddAdministratorCeremonies();
        builder.Services.AddAccounts(pilot, administrator: true, new BrowserAccountProfile(credentials.Issuer.AbsoluteUri, Client));
        ExcludeBackgroundCleanup(builder.Services);
        return builder;
    }

    /// <summary>Isolates request transaction/snapshot faults from the separately tested production cleanup worker without removing unrelated hosted services.</summary>
    private static void ExcludeBackgroundCleanup(IServiceCollection services)
    {
        var worker = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(AdministratorCleanupService));
        Assert.True(services.Remove(worker));
    }

    /// <summary>Obtains an existing issuer cookie only from a real committed enrollment grant and the production one-shot native permit.</summary>
    internal async Task SignInAsync(JourneyCompletion.Grant grant, CookieContainer cookies)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Scheme = Uri.UriSchemeHttps;
        await scope.ServiceProvider.GetRequiredService<AdministratorTicketPermit>().IssueAsync(context, grant);
        foreach (var cookie in context.Response.Headers.SetCookie)
        {
            cookies.SetCookies(issuer, cookie!);
        }
    }

    /// <summary>Uses native HTTP request/response bytes and a distinct cookie jar, with no redirect following or injected authentication principal.</summary>
    internal HttpClient Browser(CookieContainer? cookies = null) => new(new BrowserIssuerCookies(cookies ?? new(), app.GetTestServer().CreateHandler()))
    {
        BaseAddress = issuer
    };

    /// <summary>Disposes host consumers before their independently owned native credentials.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await app.DisposeAsync();
        }
        finally { credentials.Dispose(); }
    }
}

/// <summary>Applies standard browser cookie scope to TestServer transport without following credential-bearing redirects.</summary>
internal sealed class BrowserIssuerCookies(CookieContainer cookies, HttpMessageHandler transport) : DelegatingHandler(transport)
{
    private const string Cookie = "Cookie";
    private const string SetCookie = "Set-Cookie";

    /// <summary>Replays only scoped native cookies and captures server-issued replacements for subsequent explicit requests.</summary>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var header = cookies.GetCookieHeader(request.RequestUri!);
        if (!string.IsNullOrEmpty(header))
        {
            request.Headers.TryAddWithoutValidation(Cookie, header);
        }
        var result = await base.SendAsync(request, cancellationToken);
        if (result.Headers.TryGetValues(SetCookie, out var values))
        {
            foreach (var value in values)
            {
                cookies.SetCookies(request.RequestUri!, value);
            }
        }
        return result;
    }
}
