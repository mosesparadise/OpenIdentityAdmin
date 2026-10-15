using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Accounts.Login;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("issuer")]
public sealed class OperationsAcceptanceTests(IssuerFixture fixture, ITestOutputHelper output)
{
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Consumption_audit_failure_preserves_pending_or_completed_application_state(string provider, bool completed)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        using var controlled = IssuerEnvironment.CreateBrowserClient(cookies, autoRedirect: false);
        var login = await TestLogin.BeginAsync(environment, browser);
        Uri continuation;
        if (completed)
        {
            continuation = await login.CompleteAsync(environment, browser);
        }
        else
        {
            // Another genuine flow establishes an eligible session without completing this interaction.
            Assert.True((await environment.AuthorizeAsync(browser, exchange: true)).Validated);
            await using var scope = environment.Services.CreateAsyncScope();
            var row = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LoginInteraction>().AsNoTracking()
                .SingleAsync(value => value.Id == login.Interaction, TestContext.Current.CancellationToken);
            Assert.Null(row.CompletedSessionId);
            continuation = new Uri(environment.Issuer, LoginInteractions.Resume(row));
        }
        var application = await ApplicationSnapshotAsync(environment);
        var before = await ExchangeSnapshot.CaptureAsync(environment);
        await using (await RuntimeInsertDenial.CreateAsync(environment, provider))
        {
            using var denied = await controlled.GetAsync(continuation, TestContext.Current.CancellationToken);
            await SafeFailureAsync(denied, environment.Password, login.Antiforgery);
            Assert.True(string.IsNullOrEmpty(login.Flow.Code) && !login.Flow.Completion.Task.IsCompleted,
                "Audit failure must not deliver an authorization response to the relying party.");
        }
        Assert.True(application == await ApplicationSnapshotAsync(environment), "Audit failure must preserve complete application state and existing authorizations.");
        var after = await ExchangeSnapshot.CaptureAsync(environment);
        Assert.True(before.TokenIds.SetEquals(after.TokenIds), "Failed application consumption must not create native tokens.");
        before.AssertAuditUnchanged(after);
        await using (var scope = environment.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var interaction = await database.Set<LoginInteraction>().AsNoTracking().SingleAsync(value => value.Id == login.Interaction, TestContext.Current.CancellationToken);
            var request = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
                .SingleAsync(value => value.Id == interaction.RequestTokenId, TestContext.Current.CancellationToken);
            Assert.True(!interaction.Consumed && request.Status == OpenIddictConstants.Statuses.Valid && request.RedemptionDate is null,
                "Failure before native sign-in must preserve both application consumption and the native request entry.");
        }
        Assert.True((await environment.AuthorizeAsync(browser, exchange: true)).Validated);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Native_issuance_failure_keeps_code_redemption_and_real_process_logs_secret_free(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        var warmup = await TestLogin.BeginAsync(environment, browser);
        var secrets = new List<string> { environment.Username, environment.Password, environment.ClientSecret, warmup.Antiforgery,
            warmup.Flow.State, warmup.Flow.Nonce, warmup.Flow.Verifier, warmup.Flow.BrowserSecret,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(PilotProfile.ClientId + ":" + environment.ClientSecret)) };
        secrets.AddRange(cookies.GetAllCookies().Cast<Cookie>().Select(value => value.Value));
        var rejectedPassword = environment.Password + "-rejected";
        secrets.Add(rejectedPassword);
        using (var denied = await warmup.SubmitAsync(environment, browser, password: rejectedPassword))
        {
            Assert.Equal(HttpStatusCode.OK, denied.StatusCode);
            var body = await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            SafeAssertions.ContainsNoSensitiveMaterial(body, rejectedPassword);
            Assert.True(body.Contains("Unable to sign in. Check your details or start a new sign-in request.", StringComparison.Ordinal), "Rejected password must use the generic form error.");
        }
        var continuation = await warmup.CompleteAsync(environment, browser);
        var requestUri = QueryHelpers.ParseQuery(continuation.Query)["request_uri"].ToString();
        Assert.True(!string.IsNullOrEmpty(requestUri), "The genuine continuation must carry a native cached request URI.");
        secrets.Add(requestUri);
        secrets.AddRange(cookies.GetAllCookies().Cast<Cookie>().Select(value => value.Value));
        await warmup.FollowAsync(environment, browser, continuation);
        secrets.Add(warmup.Flow.Code);
        var rejectedVerifier = environment.Password;
        secrets.Add(rejectedVerifier);
        using (var denied = await environment.RelyingParty.ExchangeAsync(warmup.Flow.Code, rejectedVerifier, cancellationToken: TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
            var body = await denied.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            SafeAssertions.ContainsNoSensitiveMaterial(body, warmup.Flow.Code, rejectedVerifier);
            using var denial = JsonDocument.Parse(body);
            Assert.True(denial.RootElement.GetProperty("error").GetString() == "invalid_grant" &&
                !denial.RootElement.TryGetProperty("access_token", out _) && !denial.RootElement.TryGetProperty("id_token", out _),
                "Incorrect S256 proof must deny issuance before the successful exchange.");
        }
        using (var success = await environment.RelyingParty.ExchangeAsync(warmup.Flow.Code, warmup.Flow.Verifier, cancellationToken: TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, success.StatusCode);
            using var tokens = JsonDocument.Parse(await success.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Empty(await environment.RelyingParty.ValidateAsync(tokens.RootElement, warmup.Flow, TestContext.Current.CancellationToken));
            secrets.Add(tokens.RootElement.GetProperty("access_token").GetString()!);
            secrets.Add(tokens.RootElement.GetProperty("id_token").GetString()!);
        }
        var code = await ExchangeEvidence.IssueAsync(environment, browser);
        secrets.AddRange([code.Flow.Code, code.Flow.Verifier, code.Flow.State, code.Flow.Nonce]);
        var before = await ExchangeSnapshot.CaptureAsync(environment);
        string incident;
        await using (await RuntimeInsertDenial.CreateAsync(environment, provider, nativeTokens: true))
        {
            using var denied = await environment.RelyingParty.ExchangeAsync(code.Flow.Code, code.Flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
            incident = await SafeFailureAsync(denied, secrets.ToArray());
        }
        await using (var scope = environment.Services.CreateAsyncScope())
        {
            var entry = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<OpenIddictEntityFrameworkCoreToken>()
                .AsNoTracking().SingleAsync(value => value.Id == code.CodeId, TestContext.Current.CancellationToken);
            Assert.True(entry.Status == OpenIddictConstants.Statuses.Redeemed && entry.RedemptionDate is not null,
                "Native code redemption survives a later token-INSERT failure.");
        }
        var after = await ExchangeSnapshot.CaptureAsync(environment);
        Assert.True(before.TokenIds.SetEquals(after.TokenIds), "Denied token INSERT must not add access or identity entries.");
        before.AssertAuditUnchanged(after);
        // The correlated fault event follows all prior successful form/token handling in the actual process stream.
        await IssuerLogEvidence.AssertSafeAsync(environment, incident, secrets.Where(value => !string.IsNullOrEmpty(value)).SelectMany(value => new[] { value, Uri.EscapeDataString(value), WebUtility.HtmlEncode(value) }).Distinct().ToArray());
        await RecoverAsync(environment, browser, code);
    }

    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public async Task Actual_database_contention_is_bounded_and_client_cancellation_drains_native_work(string provider, bool cancelClient)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var code = await ExchangeEvidence.IssueAsync(environment, browser);
        var before = await ExchangeSnapshot.CaptureAsync(environment);
        var application = await ApplicationSnapshotAsync(environment);
        var elapsed = new Stopwatch();
        await using (var held = await DatabaseRowLock.AcquireNativeCodeAsync(environment, provider, code.CodeId))
        {
            using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            elapsed.Start();
            var request = environment.RelyingParty.ExchangeAsync(code.Flow.Code, code.Flow.Verifier, cancellationToken: caller.Token);
            DatabaseRowLock.BlockedCommand? blocked = null;
            try
            {
                blocked = await held.WaitForRuntimeAsync(TestContext.Current.CancellationToken);
                if (cancelClient)
                {
                    caller.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { using var response = await request; });
                }
                else
                {
                    using var response = await request;
                    if (blocked.IsUpdate)
                    {
                        // TryRedeemAsync catches the provider UPDATE failure and returns a native denial.
                        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                        SafeAssertions.ContainsNoSensitiveMaterial(text, code.Flow.Code, code.Flow.Verifier);
                        using var denial = JsonDocument.Parse(text);
                        Assert.True(denial.RootElement.GetProperty("error").GetString() == "invalid_grant" &&
                            !denial.RootElement.TryGetProperty("access_token", out _) && !denial.RootElement.TryGetProperty("id_token", out _),
                            "A failed native redemption UPDATE must deny issuance.");
                    }
                    else
                    {
                        await SafeFailureAsync(response, code.Flow.Code, code.Flow.Verifier);
                    }
                }
            }
            finally
            {
                caller.Cancel();
                try
                {
                    try
                    {
                        (await request).Dispose();
                    }
                    catch (OperationCanceledException) when (caller.IsCancellationRequested) { }
                }
                finally
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                    if (blocked is not null)
                    {
                        try
                        {
                            await held.DrainAsync(blocked, cleanup.Token);
                        }
                        catch { environment.Invalidate(); }
                    }
                    else
                    {
                        environment.Invalidate();
                    }
                }
            }
            elapsed.Stop();
            Assert.False(environment.Invalidated, "The issuer database operation did not drain before cleanup.");
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(11), "Blocked native work must finish within the recorded command/request bound.");
            output.WriteLine("Provider={0}; cancelled={1}; blockedStage={2}; drainedMilliseconds={3:F0}",
                provider, cancelClient, blocked.IsUpdate ? "update" : "read", elapsed.Elapsed.TotalMilliseconds);
        }
        // Fresh contexts, after the held transaction and native work have ended, are the persistence oracle.
        await code.RequireFreshAsync(environment);
        Assert.True(application == await ApplicationSnapshotAsync(environment), "Faulted exchange must preserve application state.");
        var after = await ExchangeSnapshot.CaptureAsync(environment);
        Assert.True(before.TokenIds.SetEquals(after.TokenIds), "Blocked exchange must not create token entries.");
        before.AssertAuditUnchanged(after);
        await RecoverAsync(environment, browser, code);
    }

    private static async Task<string> SafeFailureAsync(HttpResponseMessage response, params string[] secrets)
    {
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.True(response.Headers.Location is null, "Infrastructure failure must not redirect.");
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        SafeAssertions.ContainsNoSensitiveMaterial(body, secrets);
        Assert.True(body == "Sign-in is temporarily unavailable. Return to your application and try again.", "Unexpected infrastructure failure response.");
        var incident = response.Headers.GetValues("X-Identity-Incident").Single();
        Assert.True(Guid.TryParseExact(incident, "N", out var id) && id != Guid.Empty, "Expected an independent incident identifier.");
        return incident;
    }

    private static async Task<string> ApplicationSnapshotAsync(IssuerEnvironment environment)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return JsonSerializer.Serialize(new
        {
            Sessions = await database.Set<LocalIdentitySession>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(TestContext.Current.CancellationToken),
            Interactions = await database.Set<LoginInteraction>().AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(TestContext.Current.CancellationToken),
            Authorizations = await database.Set<OpenIddictEntityFrameworkCoreAuthorization>().OrderBy(value => value.Id).Select(value => value.Id).ToArrayAsync(TestContext.Current.CancellationToken)
        });
    }

    private static async Task RecoverAsync(IssuerEnvironment environment, HttpClient browser, ExchangeEvidence prior)
    {
        var recovery = await ExchangeEvidence.IssueAsync(environment, browser);
        Assert.True(recovery.CodeId != prior.CodeId && recovery.AuthorizationId != prior.AuthorizationId, "Recovery must use a fresh authorization, not retry the faulted code.");
        using var response = await environment.RelyingParty.ExchangeAsync(recovery.Flow.Code, recovery.Flow.Verifier, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var tokens = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await environment.RelyingParty.ValidateAsync(tokens.RootElement, recovery.Flow, TestContext.Current.CancellationToken));
    }
}
