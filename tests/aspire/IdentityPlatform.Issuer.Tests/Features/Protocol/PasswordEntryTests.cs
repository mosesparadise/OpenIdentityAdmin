using System.Net;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("interaction-acceptance")]
public sealed class PasswordEntryTests(IssuerFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly string[] Restrictions = ["disabled", "locked", "lockout-disabled", "mfa", "role", "unowned"];
    public static IEnumerable<TheoryDataRow<string, string>> Cases =>
        from provider in Providers
        from restriction in Restrictions
        select new TheoryDataRow<string, string>(provider, restriction).WithTrait("Provider", provider);

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Password_entry_rechecks_committed_account_eligibility(string provider, string restriction)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        var login = await TestLogin.BeginAsync(environment, browser);
        await using var runtime = environment.RuntimeServices();
        await using (var baseline = await LoginAccountState.CaptureAsync(environment))
        {
            string? extraUserId = null;
            string? extraRoleId = null;
            try
            {
                string username;
                await using (var scope = runtime.CreateAsyncScope())
                {
                    var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
                    var user = (await users.FindByNameAsync(environment.Username))!;
                    username = user.UserName!;
                    switch (restriction)
                    {
                        case "disabled":
                            user.IsEnabled = false;
                            Require(await users.UpdateAsync(user));
                            break;
                        case "locked":
                            Require(await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5)));
                            break;
                        case "lockout-disabled":
                            Require(await users.SetLockoutEnabledAsync(user, false));
                            break;
                        case "mfa":
                            Require(await users.SetTwoFactorEnabledAsync(user, true));
                            break;
                        case "role":
                            var role = new IdentityRole("entry-role-" + Guid.NewGuid().ToString("N"));
                            extraRoleId = role.Id;
                            Require(await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>().CreateAsync(role));
                            Require(await users.AddToRoleAsync(user, role.Name!));
                            break;
                        case "unowned":
                            var unowned = new PlatformUser { UserName = "unowned-" + Guid.NewGuid().ToString("N"), LockoutEnabled = true };
                            extraUserId = unowned.Id;
                            Require(await users.CreateAsync(unowned, environment.Password));
                            username = unowned.UserName;
                            break;
                        default:
                            throw new InvalidOperationException("Unknown password-entry restriction.");
                    }
                }
                var before = await EvidenceAsync(environment);
                using var response = await login.SubmitAsync(environment, browser, username: username);
                await AssertDeniedAsync(environment, login, response, cookies, before.Sessions, before.Audits);
            }
            finally
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                try
                {
                    await using var scope = runtime.CreateAsyncScope();
                    var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                    PilotDatabaseBounds.Apply(database);
                    if (extraRoleId is not null)
                    {
                        var role = await database.Roles.SingleOrDefaultAsync(value => value.Id == extraRoleId, cleanup.Token);
                        if (role is not null)
                        {
                            Require(await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>().DeleteAsync(role));
                        }
                    }
                    if (extraUserId is not null)
                    {
                        var user = await database.Users.SingleOrDefaultAsync(value => value.Id == extraUserId, cleanup.Token);
                        if (user is not null)
                        {
                            Require(await scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>().DeleteAsync(user));
                        }
                    }
                }
                catch { environment.Invalidate(); }
                Assert.False(environment.Invalidated, "Temporary account/role cleanup failed.");
            }
        }
        // A genuine successful control proves the shared account was restored.
        Assert.True((await environment.AuthorizeAsync(browser, exchange: true)).Validated);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Five_failures_lock_the_account_and_expiry_allows_a_genuine_login(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        await using var baseline = await LoginAccountState.CaptureAsync(environment);
        await using var runtime = environment.RuntimeServices();
        await using (var scope = runtime.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
            var user = (await users.FindByNameAsync(environment.Username))!;
            Require(await users.ResetAccessFailedCountAsync(user));
            Require(await users.SetLockoutEndDateAsync(user, null));
        }
        var login = await TestLogin.BeginAsync(environment, browser);
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var before = await EvidenceAsync(environment);
            using var response = await login.SubmitAsync(environment, browser, password: "incorrect-entry-synthetic-sentinel");
            await AssertDeniedAsync(environment, login, response, cookies, before.Sessions, before.Audits);
            await using var scope = runtime.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
            var user = (await users.FindByNameAsync(environment.Username))!;
            Assert.Equal(attempt < 5 ? attempt : 0, await users.GetAccessFailedCountAsync(user));
            Assert.Equal(attempt == 5, await users.IsLockedOutAsync(user));
        }
        var locked = await EvidenceAsync(environment);
        using (var response = await login.SubmitAsync(environment, browser))
        {
            await AssertDeniedAsync(environment, login, response, cookies, locked.Sessions, locked.Audits);
        }
        await using (var scope = runtime.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
            var user = (await users.FindByNameAsync(environment.Username))!;
            Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow, "The fifth failure must establish a future lockout.");
            Require(await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddSeconds(-1)));
        }
        Assert.True((await environment.AuthorizeAsync(browser, exchange: true)).Validated);
        await using (var scope = runtime.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
            var user = (await users.FindByNameAsync(environment.Username))!;
            Assert.Equal(0, await users.GetAccessFailedCountAsync(user));
            Assert.False(await users.IsLockedOutAsync(user));
        }
    }

    private static void Require(IdentityResult result) => Assert.True(result.Succeeded, "Unable to prepare or restore native account state.");

    private static async Task<(int Sessions, Guid[] Audits)> EvidenceAsync(IssuerEnvironment environment)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return (await database.Set<LocalIdentitySession>().CountAsync(TestContext.Current.CancellationToken),
            await database.Set<SecurityAuditIntent>().Select(value => value.Id).ToArrayAsync(TestContext.Current.CancellationToken));
    }

    private static async Task AssertDeniedAsync(IssuerEnvironment environment, TestLogin login, HttpResponseMessage response,
        CookieContainer cookies, int sessions, Guid[] auditIds)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        SafeAssertions.ContainsNoSensitiveMaterial(html, environment.Password, environment.ClientSecret, "incorrect-entry-synthetic-sentinel");
        Assert.True(html.Contains("Unable to sign in. Check your details or start a new sign-in request.", StringComparison.Ordinal), "Expected the generic credential denial.");
        Assert.False(cookies.GetCookies(environment.Issuer).Cast<Cookie>().Any(value => value.Name == PilotProfile.SessionCookie), "Denied credentials issued an authentication cookie.");
        Assert.True(string.IsNullOrEmpty(login.Flow.Code), "Denied credentials issued a code.");
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(sessions, await database.Set<LocalIdentitySession>().CountAsync(TestContext.Current.CancellationToken));
        var interaction = await database.Set<LoginInteraction>().AsNoTracking().SingleAsync(value => value.Id == login.Interaction, TestContext.Current.CancellationToken);
        Assert.True(interaction.CompletedSessionId is null && interaction.CompletedUserId is null && !interaction.Consumed, "Denied credentials changed interaction completion.");
        var added = await database.Set<SecurityAuditIntent>().Where(value => !auditIds.Contains(value.Id)).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.True(added.Length == 1 && added[0].Event == "password-verification" && added[0].Outcome == "denied" &&
            added[0].ActorType == "anonymous" && added[0].ActorId is null && added[0].CorrelationId != Guid.Empty, "Expected one anonymous denial audit.");
    }
}
