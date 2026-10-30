using System.Net;
using System.Text;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.Applications;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using BffContracts = IdentityPlatform.Admin.Bff.Features.ApiContracts.Setup;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Combines genuine native product login with a TestServer BFF and a real TLS upstream; browser-engine behavior remains separate.</summary>
internal sealed class ApplicationTransportFixture(BrowserLoginFixture login, ApplicationApiHost api, BrowserLoginHost peer, HttpClient browser, ApplicationMutationProbe probe) : IAsyncDisposable
{
    internal const string Collection = "/bff/api/v1/applications";
    internal const string TokenPath = Collection + "/antiforgery";
    internal const string CsrfHeader = "X-CSRF-TOKEN";
    private const string VersionHeader = "If-Match";
    private const string Origin = "Origin";
    private const string Media = "application/json";
    private const string TokenField = "requestToken";
    internal BrowserLoginFixture Login => login;
    internal HttpClient Browser => browser;
    internal ApplicationMutationProbe Probe => probe;
    internal DateTimeOffset Now => login.Issuer.Account.Journey.Clock.Now;

    /// <summary>Completes real password/MFA/native code exchange before attaching a separately owned BFF instance sharing only protected durable state.</summary>
    internal static async Task<ApplicationTransportFixture> CreateAsync(BootstrapFixture fixture, string provider)
    {
        var login = await BrowserLoginFixture.CreateAsync(fixture, provider);
        ApplicationApiHost? api = null;
        BrowserLoginHost? peer = null;
        try
        {
            using var completed = await login.Browser.GetAsync(await login.CallbackAsync(), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.SeeOther, completed.StatusCode);
            var probe = new ApplicationMutationProbe { Now = login.Issuer.Account.Journey.Clock.Now };
            api = await ApplicationApiHost.CreateAsync(login.Issuer.Account.Journey, provider, login.Profile.Issuer,
                login.Profile.Callback.AbsoluteUri, mutationProbe: probe);
            var destination = api.Address;
            peer = await login.PeerAsync(services => ApplicationTransportStartup.Configure(services, destination));
            var browser = peer.Browser(login.Cookies);
            var result = new ApplicationTransportFixture(login, api, peer, browser, probe);
            await result.RefreshCredentialAsync();
            return result;
        }
        catch
        {
            if (peer is not null)
            {
                await peer.DisposeAsync();
            }
            if (api is not null)
            {
                await api.DisposeAsync();
            }
            await login.DisposeAsync();
            throw;
        }
    }

    /// <summary>Obtains the genuine server-held credential only for equality observation; it is never sent through a browser header or printed.</summary>
    internal async Task RefreshCredentialAsync()
    {
        await using var scope = peer.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>().Set<AdminSessionTicket>().AsNoTracking()
            .SingleAsync(value => value.RevokedAt == null, TestContext.Current.CancellationToken);
        var ticket = scope.ServiceProvider.GetRequiredService<SessionTicketCodec>().Unprotect(row)!;
        var access = BrowserAccessCredential.Read(ticket.Properties, Now)!;
        probe.Access = access.Value;
        probe.Now = Now;
    }

    /// <summary>Calls the actual native acquisition handler over the protected browser cookie jar without changing durable session state.</summary>
    internal async Task<string> TokenAsync()
    {
        using var response = await browser.GetAsync(TokenPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Single(json.RootElement.EnumerateObject());
        return json.RootElement.GetProperty(TokenField).GetString()!;
    }

    /// <summary>Sends exact independent JSON and provenance while configuring the upstream witness to compare all forwarded fields.</summary>
    internal async Task<HttpResponseMessage> SendAsync(string csrf, HttpMethod method, string path, string body, string? version = null, string? origin = null)
    {
        probe.ExpectedMethod = method.Method;
        probe.ExpectedPath = path.Replace(Collection, ApplicationMutationRequest.ApiPath, StringComparison.Ordinal);
        probe.ExpectedBody = body;
        probe.ExpectedVersion = version;
        using var request = new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, Media) };
        request.Headers.Add(CsrfHeader, csrf);
        request.Headers.Add(Origin, origin ?? login.Profile.ClientUri.GetLeftPart(UriPartial.Authority));
        if (version is not null)
        {
            request.Headers.TryAddWithoutValidation(VersionHeader, version);
        }
        return await browser.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Joins downstream/client hosts before disposing their shared synthetic source and protection owners.</summary>
    public async ValueTask DisposeAsync()
    {
        browser.Dispose();
        try
        {
            await peer.DisposeAsync();
        }
        finally { try { await api.DisposeAsync(); } finally { await login.DisposeAsync(); } }
    }
}

/// <summary>Adds only the explicit test transport routes; production mutation routes remain dependent on T009/T016 API implementation.</summary>
internal sealed class ApplicationTransportStartup(Uri api) : IStartupFilter
{
    private const string TitleSetting = "ApiContracts:Metadata:Title";
    private const string DescriptionSetting = "ApiContracts:Metadata:Description";
    private const string Title = "Application mutation transport test";
    private const string Description = "Native JSON protection and fixed TLS proxy.";

    /// <summary>Reuses the ordinary BFF composition and native metadata with one explicit trusted upstream.</summary>
    internal static void Configure(IServiceCollection services, Uri api)
    {
        BffContracts.AddApiContracts(services, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [TitleSetting] = Title,
            [DescriptionSetting] = Description
        }).Build());
        services.Configure<IdentityPlatform.Admin.Bff.Features.ApiContracts.OpenApiMetadataOptions>(options => { options.Title = Title; options.Description = Description; });
        services.AddApplicationProxy(api);
        services.AddSingleton<IStartupFilter>(new ApplicationTransportStartup(api));
    }

    /// <summary>Installs the same token and mutation handlers ahead of the test host's ordinary terminal404.</summary>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        ApplicationClientSetup.Use(app, api, mutations: true);
        next(app);
    };
}
