using System.Net;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Uses genuine host-issued cookies and native account forms while keeping all synthetic secret values in memory only.</summary>
internal static class GovernanceHttpAuthority
{
    private const int LocalSessionSeconds = 1800;
    private const int EarlierFailures = 2;

    /// <summary>Clones the originally issued authentication cookie before a response can remove or replace it.</summary>
    internal static CookieContainer PreserveCookie(AdministratorEnvironment environment, CookieContainer source)
    {
        var original = source.GetCookies(environment.Issuer)[AdministratorJourney.SessionCookie];
        Assert.NotNull(original);
        var saved = new CookieContainer();
        saved.Add(environment.Issuer, new Cookie(original.Name, original.Value, original.Path) { Secure = true, HttpOnly = true });
        return saved;
    }

    /// <summary>Requires exact protected-page authority from a real AuthServer response without following redirects or printing HTML.</summary>
    internal static async Task RequireAsync(HttpClient browser, bool authorized)
    {
        using var response = await browser.GetAsync(AdministratorJourney.Security, TestContext.Current.CancellationToken);
        Assert.Equal(authorized ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Seeds only a local-session storage witness and prior target failures; full administrator authority already came from actual HTTP enrollment.</summary>
    internal static async Task SeedLocalAsync(GovernanceHttpBridge bridge)
    {
        await using var scope = bridge.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var actor = bridge.Original.UserId;
        var token = TestContext.Current.CancellationToken;
        var user = await database.Users.AsNoTracking().SingleAsync(row => row.Id == actor, token);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        database.Add(new LocalIdentitySession
        {
            Id = CeremonySecrets.Create(),
            UserId = actor,
            SecurityStamp = user.SecurityStamp!,
            AuthenticatedAt = now,
            ExpiresAt = now + LocalSessionSeconds
        });
        await database.SaveChangesAsync(token);
        Assert.Equal(1, await database.Set<AdministratorAuthentication>().Where(row => row.UserId == actor)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Failures, EarlierFailures), token));
    }

    /// <summary>Submits the consumed interaction with fresh antiforgery and a still-valid unused factor; rendering its locator alone is not authority.</summary>
    internal static async Task RequireConsumedContinuationAsync(HttpClient browser, Uri continuation, GovernanceHttpBridge bridge)
    {
        var form = await AdministratorJourney.ReadAsync(browser, continuation, enrollment: false);
        var code = await bridge.UnusedAsync(bridge.Original);
        var before = await bridge.SnapshotAsync();
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var denied = await AdministratorJourney.ConfirmAsync(browser, form, code, enrollment: false);
        FactorOracle.RequireWindow(bridge.Original.Seed, code, started, true);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Null(denied.Headers.Location);
        Assert.Equal(before, await bridge.SnapshotAsync());
        await RequireAsync(browser, authorized: false);
    }

    /// <summary>Proves reactivation requires fresh password plus native current factor, and only then permits protected HTTP access.</summary>
    internal static async Task LoginAsync(AdministratorEnvironment environment, GovernanceHttpBridge bridge)
    {
        using var browser = environment.Browser(new CookieContainer());
        var continuation = await AdministratorJourney.BeginAsync(environment, browser, enrollment: false);
        await RequireAsync(browser, authorized: false);
        var form = await AdministratorJourney.ReadAsync(browser, continuation, enrollment: false);
        var code = await bridge.UnusedAsync(bridge.Original);
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var completed = await AdministratorJourney.ConfirmAsync(browser, form, code, enrollment: false);
        FactorOracle.RequireWindow(bridge.Original.Seed, code, started, true);
        Assert.Equal(HttpStatusCode.Redirect, completed.StatusCode);
        Assert.Equal(AdministratorJourney.Security, completed.Headers.Location?.OriginalString);
        await RequireAsync(browser, authorized: true);
    }
}
