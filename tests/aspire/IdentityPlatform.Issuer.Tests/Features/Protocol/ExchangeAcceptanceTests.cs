using System.Net;
using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("exchange-acceptance")]
public sealed class ExchangeAcceptanceTests(IssuerFixture fixture)
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Deleted_code_subject_cannot_exchange_in_owned_disposable_environment(string provider)
    {
        await using var environment = await IssuerEnvironment.CreateAsync(provider, TestContext.Current.CancellationToken);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var code = await ExchangeEvidence.IssueAsync(environment, browser);
        await using var runtime = environment.RuntimeServices();
        await using (var scope = runtime.CreateAsyncScope())
        {
            PilotDatabaseBounds.Apply(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
            var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
            var user = await users.FindByIdAsync(code.Subject);
            Assert.NotNull(user);
            Require(await users.DeleteAsync(user));
        }
        await RequireDeletedUserAsync(environment, code.Subject);
        await code.RequireFreshAsync(environment);
        var before = await ExchangeSnapshot.CaptureAsync(environment);
        using var response = await environment.RelyingParty.ExchangeAsync(code.Flow.Code, code.Flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        await code.AssertDeniedAsync(environment, response, before);
        await RequireDeletedUserAsync(environment, code.Subject);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Committed_role_addition_invalidates_code_without_changing_security_stamp(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var code = await ExchangeEvidence.IssueAsync(environment, browser);
        await using var runtime = environment.RuntimeServices();
        await using (var baseline = await LoginAccountState.CaptureAsync(environment))
        {
            var role = new IdentityRole("exchange-role-" + Guid.NewGuid().ToString("N"));
            try
            {
                await using (var scope = runtime.CreateAsyncScope())
                {
                    PilotDatabaseBounds.Apply(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
                    var users = scope.ServiceProvider.GetRequiredService<UserManager<PlatformUser>>();
                    var user = await users.FindByIdAsync(code.Subject);
                    Assert.NotNull(user);
                    Require(await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>().CreateAsync(role));
                    Require(await users.AddToRoleAsync(user, role.Name!));
                }
                await RequireRoleAsync(environment, code, role.Id);
                await code.RequireFreshAsync(environment);
                var before = await ExchangeSnapshot.CaptureAsync(environment);
                using var response = await environment.RelyingParty.ExchangeAsync(code.Flow.Code, code.Flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
                await code.AssertDeniedAsync(environment, response, before);
                await RequireRoleAsync(environment, code, role.Id);
            }
            finally
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                try
                {
                    await using var scope = runtime.CreateAsyncScope();
                    var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
                    PilotDatabaseBounds.Apply(database);
                    var current = await database.Roles.SingleOrDefaultAsync(value => value.Id == role.Id, deadline.Token);
                    if (current is not null)
                    {
                        Require(await scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>().DeleteAsync(current));
                    }
                }
                catch { environment.Invalidate(); }
                Assert.False(environment.Invalidated, "Temporary role cleanup failed.");
            }
        }
        using var recovery = IssuerEnvironment.CreateBrowserClient();
        Assert.True((await environment.AuthorizeAsync(recovery, exchange: true)).Validated);
    }

    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Native_authorization_revocation_or_deletion_prevents_exchange(string provider, bool delete)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var code = await ExchangeEvidence.IssueAsync(environment, browser);
        await code.RequireFreshAsync(environment);
        await using var runtime = environment.RuntimeServices();
        await using (var scope = runtime.CreateAsyncScope())
        {
            PilotDatabaseBounds.Apply(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
            var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
            var authorization = await authorizations.FindByIdAsync(code.AuthorizationId, TestContext.Current.CancellationToken);
            Assert.NotNull(authorization);
            if (delete)
            {
                await authorizations.DeleteAsync(authorization, TestContext.Current.CancellationToken);
            }
            else
            {
                Assert.True(await authorizations.TryRevokeAsync(authorization, TestContext.Current.CancellationToken));
            }
        }
        await RequireAuthorizationAsync(environment, code, delete);
        // Native deletion removes linked tokens; the rejection baseline must follow that mutation.
        if (!delete)
        {
            await code.RequireFreshAsync(environment);
        }
        await code.RequireSessionAsync(environment);
        var before = await ExchangeSnapshot.CaptureAsync(environment);
        using var response = await environment.RelyingParty.ExchangeAsync(code.Flow.Code, code.Flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        await code.AssertDeniedAsync(environment, response, before);
        await RequireAuthorizationAsync(environment, code, delete);
        using var recovery = IssuerEnvironment.CreateBrowserClient();
        Assert.True((await environment.AuthorizeAsync(recovery, exchange: true)).Validated);
    }

    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Unrelated_genuine_cookie_cannot_replace_or_rescue_the_code_subject(string provider, bool codeEligible)
    {
        var environment = await fixture.GetAsync(provider);
        using var browserA = IssuerEnvironment.CreateBrowserClient();
        var code = await ExchangeEvidence.IssueAsync(environment, browserA);
        await using (var alternate = await TemporaryPilotOwner.CreateAsync(environment))
        {
            var cookies = new CookieContainer();
            using var browserB = IssuerEnvironment.CreateBrowserClient(cookies);
            var loginB = await TestLogin.BeginAsync(environment, browserB, exchange: true);
            await loginB.FollowAsync(environment, browserB, await loginB.CompleteAsync(environment, browserB, alternate.Username));
            Assert.True(loginB.Flow.Validated && alternate.UserId != code.Subject && loginB.Flow.ExpectedSessionId != code.SessionId,
                "The unrelated cookie must originate from a distinct genuinely validated login.");
            if (codeEligible)
            {
                await alternate.RestoreOwnershipAsync(TestContext.Current.CancellationToken);
            }
            await RequireEligibilityAsync(environment, code.Subject, code.SessionId, codeEligible);
            await RequireEligibilityAsync(environment, alternate.UserId, loginB.Flow.ExpectedSessionId!, !codeEligible);
            var applicationCookie = cookies.GetCookies(environment.Issuer)[PilotProfile.SessionCookie];
            Assert.True(applicationCookie is not null && !string.IsNullOrEmpty(applicationCookie.Value), "The second browser must have a genuine application cookie.");
            var header = PilotProfile.SessionCookie + "=" + applicationCookie!.Value;
            using var observer = new ObserveTokenCookie(header);
            using var transport = new HttpClient(observer) { Timeout = TimeSpan.FromSeconds(12) };
            await code.RequireFreshAsync(environment);
            var before = await ExchangeSnapshot.CaptureAsync(environment);
            using var response = await environment.RelyingParty.ExchangeAsync(code.Flow.Code, code.Flow.Verifier,
                cancellationToken: TestContext.Current.CancellationToken, transport: transport, cookie: header);
            Assert.True(observer.Attempts == 1 && observer.Matched, "The token POST must carry the exact unrelated application cookie.");
            if (codeEligible)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                using var tokens = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
                Assert.Empty(await environment.RelyingParty.ValidateAsync(tokens.RootElement, code.Flow, TestContext.Current.CancellationToken));
                before.AssertAuditUnchanged(await ExchangeSnapshot.CaptureAsync(environment));
            }
            else
            {
                await code.AssertDeniedAsync(environment, response, before);
            }
            await code.RequireSessionAsync(environment);
            await RequireEligibilityAsync(environment, code.Subject, code.SessionId, codeEligible);
            await RequireEligibilityAsync(environment, alternate.UserId, loginB.Flow.ExpectedSessionId!, !codeEligible);
        }
        using var recovery = IssuerEnvironment.CreateBrowserClient();
        Assert.True((await environment.AuthorizeAsync(recovery, exchange: true)).Validated);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Lost_successful_response_is_not_retried_and_recovery_requires_new_authorization(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var code = await ExchangeEvidence.IssueAsync(environment, browser);
        var before = await ExchangeSnapshot.CaptureAsync(environment);
        using var discard = new DiscardTokenResponse();
        using var transport = new HttpClient(discard) { Timeout = TimeSpan.FromSeconds(12) };
        var failure = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            using var unexpected = await environment.RelyingParty.ExchangeAsync(code.Flow.Code, code.Flow.Verifier,
                cancellationToken: TestContext.Current.CancellationToken, transport: transport);
        });
        Assert.True(failure.Message == "Synthetic token-response delivery failure." && discard.Attempts == 1 && discard.SuccessfulResponseDiscarded,
            "Only one genuinely successful drained and disposed token response may trigger the injected delivery failure.");
        var issued = await RequireIssuedAsync(environment, code, before);
        var afterLoss = await ExchangeSnapshot.CaptureAsync(environment);
        before.AssertAuditUnchanged(afterLoss);
        // Deliberate adversarial replay, separate from normal application recovery.
        using var replay = await environment.RelyingParty.ExchangeAsync(code.Flow.Code, code.Flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        await code.AssertDeniedAsync(environment, replay, afterLoss);
        await using (var scope = environment.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var entries = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
                .Where(value => issued.Contains(value.Id!)).ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.True(entries.Length == 2 && entries.All(value => value.Status == OpenIddictConstants.Statuses.Revoked),
                "Replay must revoke the exact issued access and identity entries.");
        }
        var recovery = await ExchangeEvidence.IssueAsync(environment, browser);
        Assert.True(recovery.CodeId != code.CodeId && recovery.AuthorizationId != code.AuthorizationId, "Recovery must use a distinct new code and authorization.");
        using var response = await environment.RelyingParty.ExchangeAsync(recovery.Flow.Code, recovery.Flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var tokens = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await environment.RelyingParty.ValidateAsync(tokens.RootElement, recovery.Flow, TestContext.Current.CancellationToken));
        Assert.Equal(1, discard.Attempts);
    }

    private static async Task<HashSet<string>> RequireIssuedAsync(IssuerEnvironment environment, ExchangeEvidence code, ExchangeSnapshot before)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var entries = await ExchangeEvidence.Tokens(database).ToArrayAsync(TestContext.Current.CancellationToken);
        var redeemed = entries.Single(value => value.Id == code.CodeId);
        Assert.True(redeemed.Status == OpenIddictConstants.Statuses.Redeemed && redeemed.RedemptionDate is not null, "The exact native code must be redeemed after response loss.");
        var added = entries.Where(value => !before.TokenIds.Contains(value.Id!)).ToArray();
        Assert.True(added.Length == 2 && added.All(value => value.Subject == code.Subject && value.Application?.Id == code.ApplicationId &&
            value.Authorization?.Id == code.AuthorizationId && value.Status == OpenIddictConstants.Statuses.Valid) &&
            added.Count(value => value.Type == OpenIddictConstants.TokenTypeIdentifiers.AccessToken) == 1 &&
            added.Count(value => value.Type == OpenIddictConstants.TokenTypeIdentifiers.IdentityToken) == 1,
            "One successful lost response must issue exactly one bound access and identity entry.");
        return added.Select(value => value.Id!).ToHashSet(StringComparer.Ordinal);
    }

    private static async Task RequireEligibilityAsync(IssuerEnvironment environment, string subject, string session, bool eligible)
    {
        await using var runtime = environment.RuntimeServices();
        await using var scope = runtime.CreateAsyncScope();
        PilotDatabaseBounds.Apply(scope.ServiceProvider.GetRequiredService<PlatformDbContext>());
        var result = await scope.ServiceProvider.GetRequiredService<PilotEligibility>().SessionAsync(session, subject, TestContext.Current.CancellationToken);
        Assert.True((result is not null) == eligible, "The intended current subject/session eligibility must hold at exchange.");
    }

    private static async Task RequireDeletedUserAsync(IssuerEnvironment environment, string subject)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Users.AnyAsync(value => value.Id == subject, TestContext.Current.CancellationToken));
    }

    private static async Task RequireRoleAsync(IssuerEnvironment environment, ExchangeEvidence code, string role)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await database.UserRoles.AnyAsync(value => value.UserId == code.Subject && value.RoleId == role, TestContext.Current.CancellationToken),
            "The intended native role membership must remain committed.");
        await code.RequireSessionAsync(environment);
    }

    private static async Task RequireAuthorizationAsync(IssuerEnvironment environment, ExchangeEvidence code, bool deleted)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var authorization = await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == code.AuthorizationId, TestContext.Current.CancellationToken);
        Assert.True(deleted ? authorization is null : authorization?.Status == OpenIddictConstants.Statuses.Revoked,
            "The intended native authorization mutation must remain committed.");
        if (deleted)
        {
            Assert.False(await database.Set<OpenIddictEntityFrameworkCoreToken>().AnyAsync(value => value.Id == code.CodeId, TestContext.Current.CancellationToken));
        }
    }

    private static void Require(IdentityResult result) => Assert.True(result.Succeeded, "The native identity mutation must succeed.");
}
