using System.Text.Encodings.Web;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.AuthServer.Features.Issuer.Credentials;
using IdentityPlatform.AuthServer.Features.Protocol;
using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Migrator.Features.AdministratorProtocol;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Composes real account enrollment, its one-shot cookie permit and installed native server handlers with durable fixture keys.</summary>
internal sealed class AdministratorProtocolScenario(GovernanceScenario account, NativeAssertionMaterial material) : IAsyncDisposable
{
    private const string Operator = "native-account-protocol-fixture";
    private const int ArtifactSeconds = 60;
    private const string CookieSeparator = "; ";
    internal const string AuthorizePath = "/connect/authorize";
    internal const string TokenPath = "/connect/token";
    internal const string ParPath = "/connect/par";
    internal GovernanceScenario Account => account;
    internal IServiceProvider Services => account.Journey.Get<IServiceProvider>();
    internal NativeAssertionMaterial Material => material;
    internal Uri Issuer => Services.GetRequiredService<IssuerCredentials>().Issuer;
    internal string Cookie { get; private set; } = string.Empty;

    /// <summary>Initializes actual manager-backed registration and obtains a cookie only from the completed production enrollment grant.</summary>
    internal static async Task<AdministratorProtocolScenario> CreateAsync(BootstrapFixture fixture, string provider)
    {
        var material = new NativeAssertionMaterial();
        GovernanceScenario? account = null;
        try
        {
            account = await GovernanceScenario.CreateAsync(fixture, provider, services => Configure(services, material));
            var scenario = new AdministratorProtocolScenario(account, material);
            Assert.True(await AdministratorRegistrationInitializer.RunAsync(scenario.Services, material.Policy, Operator, Guid.NewGuid(), TestContext.Current.CancellationToken));
            await scenario.IssueCookieAsync();
            return scenario;
        }
        catch
        {
            if (account is not null)
            {
                await account.DisposeAsync();
            }
            material.Dispose();
            throw;
        }
    }

    /// <summary>Uses the same protected issuer material as the journey; neither ephemeral server keys nor a synthetic authentication handler are installed.</summary>
    private static void Configure(IServiceCollection services, NativeAssertionMaterial material)
    {
        var credentials = (IssuerCredentials)services.Last(descriptor => descriptor.ServiceType == typeof(IssuerCredentials)).ImplementationInstance!;
        services.AddSingleton(UrlEncoder.Default).AddSingleton(material.Policy).AddProtocolStorageOwnership();
        AccountSchemes.AddAdministrator(services, pilot: false);
        services.AddOptions<CookieAuthenticationOptions>(AccountSchemes.Administrator).Configure<TimeProvider>((options, clock) => options.TimeProvider = clock);
        services.AddProtocol(credentials, new ProtocolProfiles(false, material.Policy));
        var server = services.AddOpenIddict().AddServer();
        NativeAccountClockAdvance.Configure(server);
        NativeAccountResponseFault.Configure(server);
        NativeAccountFinalRead.Configure(server);
        server.AddEventHandler<OpenIddictServerEvents.ProcessRequestContext>(handler => handler.SetOrder(int.MinValue)
            .UseInlineHandler(NativeAccountHttp.Remember));
        services.AddOptions<OpenIddictServerOptions>().Configure<TimeProvider>((options, clock) =>
        {
            options.TimeProvider = clock;
            options.RequestTokenLifetime = TimeSpan.FromSeconds(ArtifactSeconds);
        });
    }

    /// <summary>Uses the genuine named cookie handler and scoped permit, then retains only its test-owned cookie header in memory.</summary>
    private async Task IssueCookieAsync()
    {
        await using var scope = account.CreateScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider, RequestAborted = TestContext.Current.CancellationToken };
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.Host = new HostString(Issuer.Host);
        await scope.ServiceProvider.GetRequiredService<AdministratorTicketPermit>().IssueAsync(http, account.Grant);
        var cookies = http.Response.Headers.SetCookie.Where(value => value is not null &&
            value.StartsWith(AccountSchemes.AdministratorCookie, StringComparison.Ordinal)).Select(value => value!.Split(';')[0]).ToArray();
        Assert.NotEmpty(cookies);
        Cookie = string.Join(CookieSeparator, cookies);
    }

    /// <summary>Disposes all native handlers before the assertion signer and isolated database lease are released.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await account.DisposeAsync();
        }
        finally { material.Dispose(); }
    }
}
