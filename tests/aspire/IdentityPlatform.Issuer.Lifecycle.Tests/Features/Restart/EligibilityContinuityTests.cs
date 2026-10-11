using System.Net;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Restart;

[Collection("lifecycle")]
public sealed class EligibilityContinuityTests(LifecycleFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly string[] RestrictionKinds = ["revoked", "expired", "disabled"];

    public static IEnumerable<TheoryDataRow<string, string>> Restrictions =>
        from provider in Providers
        from restriction in RestrictionKinds
        select new TheoryDataRow<string, string>(provider, restriction).WithTrait("Provider", provider);

    [Theory]
    [MemberData(nameof(Restrictions))]
    public async Task Restriction_committed_while_issuer_stopped_denies_original_cookie_then_fresh_login_recovers(string provider, string restriction)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        var original = await environment.AuthorizeAsync(browser, exchange: true);
        try
        {
            await environment.RestartIssuerAsync(async cancellationToken =>
            {
                await using var scope = environment.Services.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                var session = await database.Set<LocalIdentitySession>().SingleAsync(value => value.Id == original.ExpectedSessionId, cancellationToken);
                if (restriction == "disabled")
                {
                    var manager = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
                    var user = await manager.FindByIdAsync(session.UserId);
                    user!.IsEnabled = false;
                    Assert.True((await manager.UpdateAsync(user)).Succeeded);
                }
                else
                {
                    if (restriction == "revoked")
                    {
                        session.Revoked = true;
                    }
                    else
                    {
                        session.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds();
                    }
                    await database.SaveChangesAsync(cancellationToken);
                }
            });
            var before = await ExchangeSnapshot.CaptureAsync(environment);
            await RestartEvidence.SilentAsync(environment, cookies, original, allowed: false);
            var after = await ExchangeSnapshot.CaptureAsync(environment);
            // Authorization request caching may add a request token; it must not add a code or issued token.
            await using var scope = environment.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var added = (await ExchangeEvidence.Tokens(database).ToArrayAsync(TestContext.Current.CancellationToken)).Where(value => !before.TokenIds.Contains(value.Id!));
            Assert.True(added.All(value => value.Type == OpenIddict.Abstractions.OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken), "Denied silent flow issued a code or token.");
            before.AssertAuditUnchanged(after);
            var terminal = await database.Set<LocalIdentitySession>().AsNoTracking().SingleAsync(value => value.Id == original.ExpectedSessionId, TestContext.Current.CancellationToken);
            Assert.True(restriction == "disabled" || (restriction == "revoked" ? terminal.Revoked : terminal.ExpiresAt <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                "Restart revived terminal session state.");
        }
        finally
        {
            if (restriction == "disabled")
            {
                try
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await using var scope = environment.Services.CreateAsyncScope();
                    var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                    var user = await database.Users.SingleAsync(value => value.UserName == environment.Username, cleanup.Token);
                    user.IsEnabled = true;
                    await database.SaveChangesAsync(cleanup.Token);
                }
                catch
                {
                    environment.Invalidate();
                }
            }
        }
        Assert.False(environment.Invalidated, "Account restoration failed; environment is not reusable.");
        using var recovery = IssuerEnvironment.CreateBrowserClient();
        var fresh = await environment.AuthorizeAsync(recovery, exchange: true);
        Assert.True(fresh.Validated && fresh.ExpectedSessionId != original.ExpectedSessionId, "Recovery did not establish a distinct eligible session.");
    }
}
