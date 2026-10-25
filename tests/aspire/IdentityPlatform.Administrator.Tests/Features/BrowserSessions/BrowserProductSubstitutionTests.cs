using System.Net;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Drives genuine foreign-account and expired-PAR outcomes through the product boundary without fabricating native authority.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserProductSubstitutionTests(BootstrapFixture fixture)
{
    /// <summary>A valid native cookie for another account in the same issuer cannot satisfy the completed intent's subject/session binding.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_DifferentGenuineIssuerAccount_When_SubstitutedAtProductAuthorization_Then_ErrorCallbackCreatesNoTicket(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var foreignCookie = await OtherAccountCookieAsync(scenario, provider);
        var challenge = await scenario.ChallengeAsync();
        var callback = await scenario.AuthorizeAsync(challenge, foreignCookie, error: true);
        var cookies = scenario.Cookies.GetCookieHeader(callback);
        using var denied = await scenario.Browser.GetAsync(callback, TestContext.Current.CancellationToken);
        BrowserLoginTests.RequireDenied(denied, HttpStatusCode.ServiceUnavailable);
        await BrowserCallbackTests.RequireNoTicketAsync(scenario, sends: 0);
        var before = await scenario.SnapshotAsync();
        using var replay = await BrowserCallbackTests.ReplayAsync(scenario.Host, callback, cookies);
        BrowserLoginTests.RequireDenied(replay, HttpStatusCode.ServiceUnavailable);
        Assert.Equal(before, await scenario.SnapshotAsync());
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
    }

    /// <summary>Uses actual password and MFA forms under the same issuer keys/database; no fabricated or foreign-key cookie is substituted.</summary>
    private static async Task<string> OtherAccountCookieAsync(BrowserLoginFixture scenario, string provider)
    {
        var account = scenario.Issuer.Account;
        var user = await GovernanceAccounts.AddAsync(account);
        await using var scope = account.CreateScope();
        var name = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Users.AsNoTracking()
            .Where(row => row.Id == user).Select(row => row.UserName).SingleAsync(TestContext.Current.CancellationToken);
        await using var host = await BrowserIssuerHost.CreateAsync(account.Journey, provider);
        var jar = new CookieContainer();
        using var browser = host.Browser(jar);
        using var page = await browser.GetAsync(AdministratorJourney.Login, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        using var password = await BrowserIssuerHttp.PostAsync(browser, AdministratorJourney.Login, new Dictionary<string, string>
        {
            [AdministratorJourney.Antiforgery] = AdministratorJourney.Input(await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), AdministratorJourney.Antiforgery),
            [BrowserIssuerHttp.Username] = name!,
            [BrowserIssuerHttp.Password] = GovernanceAccounts.Password(account)
        });
        Assert.Equal(HttpStatusCode.Redirect, password.StatusCode);
        var factor = await AdministratorJourney.ReadAsync(browser, password.Headers.Location!, enrollment: false);
        var code = await account.UnusedCodeAsync(user, FactorScenario.Seed);
        using var complete = await AdministratorJourney.ConfirmAsync(browser, factor, code, enrollment: false);
        Assert.Equal(HttpStatusCode.Redirect, complete.StatusCode);
        Assert.Equal(AdministratorJourney.Security, complete.Headers.Location?.OriginalString);
        Assert.NotNull(jar.GetCookies(host.Issuer)[AdministratorJourney.SessionCookie]);
        using var security = await browser.GetAsync(AdministratorJourney.Security, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, security.StatusCode);
        Assert.True(user != account.ActorId);
        Assert.True(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>()
            .Set<IdentityPlatform.Infrastructure.Features.Identity.Persistence.AdministratorSession>().AsNoTracking()
            .AnyAsync(row => row.UserId == user && !row.Revoked, TestContext.Current.CancellationToken));
        return jar.GetCookieHeader(host.Issuer);
    }

    /// <summary>The original real PAR is rejected at its stored expiry, without extending the intent, inventing a callback or starting exchange.</summary>
    [Theory, MemberData(nameof(BrowserLoginTests.Providers), MemberType = typeof(BrowserLoginTests))]
    public async Task Given_ReservedProductChallenge_When_ParExpiresBeforeAuthorization_Then_NoCallbackExchangeOrSessionIsCreated(string provider)
    {
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider);
        var challenge = await scenario.ChallengeAsync();
        var reference = QueryHelpers.ParseQuery(challenge.Query)[OpenIddictConstants.Parameters.RequestUri].ToString();
        var id = await NativeAccountPersistence.ReferenceIdAsync(scenario.Issuer, reference);
        await using var scope = scenario.Issuer.Account.CreateScope();
        var expires = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<OpenIddictEntityFrameworkCoreToken>()
            .AsNoTracking().Where(row => row.Id == id).Select(row => row.ExpirationDate).SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(expires);
        scenario.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(DateTime.SpecifyKind(expires.Value, DateTimeKind.Utc));
        var before = await scenario.SnapshotAsync();
        var denied = await scenario.AuthorizationAsync(challenge, scenario.IssuerCookies.GetCookieHeader(scenario.Profile.Issuer));
        Assert.Equal((int)HttpStatusCode.BadRequest, denied.Status);
        Assert.True(string.IsNullOrEmpty(denied.Location));
        Assert.False(denied.HasCookie);
        Assert.Equal(0, scenario.Host.Bridge.TokenSends);
        Assert.Equal(0, await scenario.ReadAsync(database => database.Set<AdminSessionTicket>().CountAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(before, await scenario.SnapshotAsync());
    }
}
