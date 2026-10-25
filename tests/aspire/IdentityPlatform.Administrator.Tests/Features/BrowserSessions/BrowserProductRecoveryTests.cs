using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Ensures genuine restricted recovery cannot satisfy the product's fresh administrator ceremony.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserProductRecoveryTests(BootstrapFixture fixture)
{
    /// <summary>Actual password and recovery-code verification grant only replacement authority; product login cannot exchange or establish from it.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_GenuineRestrictedRecovery_When_ProductSignInIsAttempted_Then_NoCompletionExchangeOrTicketIsCreated(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        await using var host = await BrowserIssuerHost.CreateAsync(scenario.Issuer.Account.Journey, provider);
        var cookies = new CookieContainer();
        using var browser = host.Browser(cookies);
        var replacement = await RecoverAsync(scenario, browser);
        Assert.Null(cookies.GetCookies(host.Issuer)[AdministratorJourney.SessionCookie]);
        await using var scope = scenario.Issuer.Account.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal("RecoveryRequired", await database.Set<AdministratorEnrollment>().AsNoTracking().Select(row => row.State).SingleAsync(TestContext.Current.CancellationToken));
        var intent = await scenario.StartAsync(await scenario.EntryAsync());
        var entry = await BrowserIssuerHttp.EntryAsync(browser, intent);
        using var denied = await BrowserIssuerHttp.PasswordAsync(browser, scenario.Issuer.Account.Journey, entry);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Null(denied.Headers.Location);
        using var resume = await scenario.ResumeAsync(replacement.Interaction);
        BrowserLoginTests.RequireDenied(resume, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(1, scenario.Host.Bridge.ParSends);
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
        Assert.Equal(0, await scenario.ReadAsync(db => db.Set<AdminSessionTicket>().CountAsync(TestContext.Current.CancellationToken)));
    }

    /// <summary>Traverses real protected password/recovery forms and stops before replacement-factor completion.</summary>
    private static async Task<AdministratorForm> RecoverAsync(BrowserLoginFixture scenario, HttpClient browser)
    {
        using var page = await browser.GetAsync(SecurityHttp.Recovery, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        using var password = await BrowserIssuerHttp.PostAsync(browser, SecurityHttp.Recovery, new Dictionary<string, string>
        {
            [AdministratorJourney.Antiforgery] = AdministratorJourney.Input(await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), AdministratorJourney.Antiforgery),
            [BrowserIssuerHttp.Username] = scenario.Issuer.Account.Journey.Lease.Username,
            [BrowserIssuerHttp.Password] = scenario.Issuer.Account.Password
        });
        Assert.Equal(HttpStatusCode.Redirect, password.StatusCode);
        var form = await AdministratorJourney.ReadAsync(browser, password.Headers.Location!, enrollment: false);
        var location = await SecurityHttp.TransitionAsync(browser, form, scenario.Issuer.Account.Grant.RecoveryCodes[0], recovery: true);
        return await AdministratorJourney.ReadAsync(browser, location, enrollment: true);
    }
}
