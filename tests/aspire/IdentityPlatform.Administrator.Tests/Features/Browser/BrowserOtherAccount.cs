using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Obtains a different administrator cookie through real browser password/MFA under the same issuer keys and database.</summary>
internal static class BrowserOtherAccount
{
    private const string Username = "#username";
    private const string Password = "#password";
    private const string Code = "#code";
    private const string Submit = "#submit";
    private const string IssuerCookie = "__Host-identity-administrator";

    /// <summary>Creates a native synthetic nominee, exercises its actual standalone forms and retains only its real cookie in memory.</summary>
    internal static async Task<(Cookie Cookie, string User)> SignInAsync(BrowserHttpsFixture fixture, IPage page)
    {
        var account = fixture.Issuer.Account;
        var user = await GovernanceAccounts.AddAsync(account);
        await using var scope = account.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var name = await database.Users.AsNoTracking().Where(row => row.Id == user).Select(row => row.UserName)
            .SingleAsync(TestContext.Current.CancellationToken);
        var response = await page.GotoAsync(new Uri(fixture.Profile.Issuer, AdministratorJourney.Login).AbsoluteUri);
        Assert.Equal(StatusCodes.Status200OK, response!.Status);
        await page.Locator(Username).FillAsync(name!);
        await page.Locator(Password).FillAsync(GovernanceAccounts.Password(account));
        await page.Locator(Submit).ClickAsync();
        await page.Locator(Code).FillAsync(await account.UnusedCodeAsync(user, FactorScenario.Seed));
        await page.Locator(Submit).ClickAsync();
        Assert.True(page.Url == new Uri(fixture.Profile.Issuer, AdministratorJourney.Security).AbsoluteUri);
        var proof = await page.ReloadAsync();
        Assert.Equal(StatusCodes.Status200OK, proof!.Status);
        Assert.True(user != account.ActorId);
        Assert.Equal(1, await database.Set<AdministratorSession>().CountAsync(row => row.UserId == user && !row.Revoked, TestContext.Current.CancellationToken));
        var matches = (await page.Context.CookiesAsync()).Where(item => item.Name == IssuerCookie).ToArray();
        var matchesCount = matches.Length;
        Assert.Equal(1, matchesCount);
        var cookie = matches[0];
        return (new Cookie
        {
            Name = cookie.Name,
            Value = cookie.Value,
            Domain = cookie.Domain,
            Path = cookie.Path,
            Secure = cookie.Secure,
            HttpOnly = cookie.HttpOnly,
            SameSite = cookie.SameSite,
            Expires = cookie.Expires
        }, user);
    }

    /// <summary>Checks the independent source remains live after the rejected B3 substitution.</summary>
    internal static async Task RequireLiveAsync(BrowserHttpsFixture fixture, string user)
    {
        await using var scope = fixture.Issuer.Account.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var source = await database.Set<AdministratorSession>().AsNoTracking().SingleAsync(row => row.UserId == user, TestContext.Current.CancellationToken);
        Assert.False(source.Revoked);
    }
}
