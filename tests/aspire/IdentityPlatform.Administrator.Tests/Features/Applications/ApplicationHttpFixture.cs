using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Drives genuine password/MFA, native network code exchange and API bearer validation over real HTTPS; it does not emulate a browser engine.</summary>
internal sealed class ApplicationHttpFixture(BrowserHttpsFixture environment, HttpClient browser, HttpClient issuer, HttpClient api) : IAsyncDisposable
{
    internal const string Collection = "/bff/api/v1/applications";
    internal const string ApiCollection = "/api/v1/applications";
    private const string TokenPath = Collection + "/antiforgery";
    private const string LoginPath = "/bff/login";
    private const string FactorCode = "Code";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string Origin = "Origin";
    private const string Media = "application/json";
    private const string TokenField = "requestToken";
    private const string Bearer = "Bearer";
    internal BrowserHttpsFixture Environment => environment;
    internal HttpClient Browser => browser;
    internal HttpClient Api => api;
    internal DateTimeOffset Now => environment.Issuer.Account.Journey.Clock.Now;
    internal string Access { private get; set; } = string.Empty;

    /// <summary>Owns actual three-host TLS composition and runs the real ceremony before obtaining any protected API credential.</summary>
    internal static async Task<ApplicationHttpFixture> CreateAsync(BootstrapFixture fixture, string provider, ApplicationWriteFault? fault = null)
    {
        var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, applications: true, configureApi: services =>
        {
            if (fault is not null)
            {
                services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(fault));
            }
        });
        var cookies = new CookieContainer();
        var browser = Client(environment.Profile.ClientUri, cookies);
        var issuer = Client(environment.Profile.Issuer, cookies);
        var api = Client(environment.Api!.Address, new CookieContainer());
        var result = new ApplicationHttpFixture(environment, browser, issuer, api);
        try
        {
            await result.SignInAsync();
            return result;
        }
        catch { await result.DisposeAsync(); throw; }
    }

    /// <summary>Uses platform TLS trust and explicit redirects without authentication replacements, retries or private-key sharing with the API.</summary>
    private static HttpClient Client(Uri address, CookieContainer cookies) => new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = cookies }) { BaseAddress = address };

    /// <summary>Follows only the actual known BFF/issuer redirect chain while submitting genuine native form proofs and unused MFA.</summary>
    private async Task SignInAsync()
    {
        using var page = await browser.GetAsync(LoginPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var csrf = AdministratorJourney.Input(await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), AdministratorJourney.Antiforgery);
        using var start = await BrowserIssuerHttp.PostAsync(browser, LoginPath, new Dictionary<string, string> { [AdministratorJourney.Antiforgery] = csrf });
        Assert.Equal(HttpStatusCode.SeeOther, start.StatusCode);
        var entry = await BrowserIssuerHttp.EntryAsync(issuer, QueryHelpers.ParseQuery(start.Headers.Location!.Query)[BrowserIssuerHttp.Intent].Single()!);
        using var password = await BrowserIssuerHttp.PasswordAsync(issuer, environment.Issuer.Account.Journey, entry);
        var factor = await BrowserIssuerHttp.FactorAsync(issuer, password, environment.Profile.ClientUri);
        factor[FactorCode] = await IssuerContinuationProviderFixture.UnusedCodeAsync(environment.Issuer.Account.Journey);
        using var complete = await BrowserIssuerHttp.PostAsync(issuer, BrowserIssuerHttp.Mfa, factor);
        Assert.Equal(HttpStatusCode.Redirect, complete.StatusCode);
        using var resume = await browser.GetAsync(complete.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, resume.StatusCode);
        using var authorize = await issuer.GetAsync(resume.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        using var callback = await browser.GetAsync(authorize.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.SeeOther, callback.StatusCode);
        await using var scope = environment.Client.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>().Set<AdminSessionTicket>().AsNoTracking()
            .SingleAsync(value => value.RevokedAt == null, TestContext.Current.CancellationToken);
        var ticket = scope.ServiceProvider.GetRequiredService<SessionTicketCodec>().Unprotect(row)!;
        Access = BrowserAccessCredential.Read(ticket.Properties, Now)!.Value;
    }

    /// <summary>Acquires the actual native application-family proof through the authenticated BFF route.</summary>
    internal async Task<string> TokenAsync()
    {
        using var response = await browser.GetAsync(TokenPath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return body.RootElement.GetProperty(TokenField).GetString()!;
    }

    /// <summary>Sends one explicit JSON mutation with genuine CSRF or actual server bearer; generated secrets never become request input.</summary>
    internal async Task<HttpResponseMessage> CreateAsync(string body, bool direct = false, bool authenticated = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, direct ? ApiCollection : Collection) { Content = new StringContent(body, Encoding.UTF8, Media) };
        if (direct)
        {
            if (authenticated)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, Access);
            }
        }
        else
        {
            request.Headers.Add(Origin, environment.Profile.ClientUri.GetLeftPart(UriPartial.Authority));
            request.Headers.Add(Csrf, await TokenAsync());
        }
        return await (direct ? api : browser).SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Sends a direct genuine bearer details read without copying that credential into the BFF request surface.</summary>
    internal async Task<HttpResponseMessage> DirectGetAsync(string path, bool authenticated = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (authenticated)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, Access);
        }
        return await api.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Observes persisted state through a separate ordinary test scope; production endpoints retain their own owned/read-only children.</summary>
    internal AsyncServiceScope Scope()
    {
        var scope = environment.Api!.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ProtocolStorageSelection>().Select(ProtocolStorageMode.LegacyPilot);
        return scope;
    }

    /// <summary>Disposes test clients before real hosts and provider leases.</summary>
    public async ValueTask DisposeAsync()
    {
        api.Dispose();
        issuer.Dispose();
        browser.Dispose();
        await environment.DisposeAsync();
    }
}
