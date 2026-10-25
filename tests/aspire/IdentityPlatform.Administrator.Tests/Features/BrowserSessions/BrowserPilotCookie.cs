using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.AuthServer.Features.Accounts.Login;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Obtains same-issuer pilot authority through real password login; initial interaction admission is explicitly fixture-owned.</summary>
internal static class BrowserPilotCookie
{
    private const string Username = "synthetic-browser-pilot";
    private const string UsernameSetting = "Pilot:UserName";
    private const string PasswordSetting = "Pilot:Password";
    private const string SecretSetting = "Pilot:ClientSecret";
    private const string RedirectSetting = "Pilot:RedirectUri";
    private const string Redirect = "https://pilot.example.test/callback";
    private const string Login = "/account/login";
    private const string Complete = "/account/complete";
    private const string Query = "?interaction=";
    private const string Interaction = "Interaction";
    private const string SyntheticReference = "urn:synthetic-pilot-reference";
    private const int NativeLifetimeSeconds = 60;

    /// <summary>Supplies disposable test configuration only; the real initializer creates native user/profile records.</summary>
    internal static void Configure(WebApplicationBuilder builder, JourneyScenario scenario) => builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        [UsernameSetting] = Username,
        [PasswordSetting] = scenario.Lease.Password,
        [SecretSetting] = Guid.NewGuid().ToString() + Guid.NewGuid().ToString(),
        [RedirectSetting] = Redirect
    });

    /// <summary>Runs actual pilot password verification and native cookie issuance, then independently checks its committed session.</summary>
    internal static async Task<string> CreateAsync(BrowserLoginFixture scenario, string provider)
    {
        await using var host = await BrowserIssuerHost.CreatePilotAsync(scenario.Issuer.Account.Journey, provider);
        var cookies = new CookieContainer();
        var interaction = await AdmitAsync(host, cookies, scenario.Issuer.Account.Journey.Clock.GetUtcNow());
        using var browser = host.Browser(cookies);
        using var page = await browser.GetAsync(Login + Query + interaction, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var antiforgery = AdministratorJourney.Input(await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), AdministratorJourney.Antiforgery);
        using var result = await BrowserIssuerHttp.PostAsync(browser, Login, new Dictionary<string, string>
        {
            [AdministratorJourney.Antiforgery] = antiforgery,
            [Interaction] = interaction,
            [BrowserIssuerHttp.Username] = Username,
            [BrowserIssuerHttp.Password] = scenario.Issuer.Account.Password
        });
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Equal(Complete + Query + interaction, result.Headers.Location?.OriginalString);
        using var complete = await browser.GetAsync(result.Headers.Location, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.NotNull(cookies.GetCookies(host.Issuer)[PilotProfile.SessionCookie]);
        Assert.Null(cookies.GetCookies(host.Issuer)[AdministratorJourney.SessionCookie]);
        await RequireSessionAsync(host);
        return cookies.GetCookieHeader(host.Issuer);
    }

    /// <summary>Uses production bounded admission and its genuine correlation cookies; this synthetic reference is not credited as a pilot OAuth round trip.</summary>
    private static async Task<string> AdmitAsync(BrowserIssuerHost host, CookieContainer cookies, DateTimeOffset now)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider, RequestAborted = TestContext.Current.CancellationToken };
        http.Request.Scheme = Uri.UriSchemeHttps;
        var interaction = await scope.ServiceProvider.GetRequiredService<LoginInteractionAdmission>()
            .CreateAsync(http, Guid.NewGuid().ToString(), SyntheticReference, now.ToUnixTimeSeconds() + NativeLifetimeSeconds);
        Assert.NotNull(interaction);
        foreach (var cookie in http.Response.Headers.SetCookie)
        {
            cookies.SetCookies(host.Issuer, cookie!);
        }
        return interaction.Id;
    }

    /// <summary>Requires the persisted pilot principal to pass the actual account eligibility reader under the same issuer root.</summary>
    private static async Task RequireSessionAsync(BrowserIssuerHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var session = await database.Set<LocalIdentitySession>().AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(await scope.ServiceProvider.GetRequiredService<PilotEligibility>().SessionAsync(session.Id, session.UserId, TestContext.Current.CancellationToken));
    }
}
