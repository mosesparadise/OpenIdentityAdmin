using System.Net;
using IdentityPlatform.AuthServer.Features.Accounts.Login;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using IdentityPlatform.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("authorization-state")]
public sealed partial class AuthorizationStateTests(IssuerFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly string[] Boundaries = ["new", "new-none", "pending-none", "completed"];
    private static readonly string[] Restrictions = ["disabled", "stamp", "session-revoked", "session-expired", "client-disabled", "permission", "scope-disabled", "scope-resource"];
    public static IEnumerable<TheoryDataRow<string, string, string>> RestrictedCases =>
        from provider in Providers
        from boundary in Boundaries
        from restriction in Restrictions
        select new TheoryDataRow<string, string, string>(provider, boundary, restriction).WithTrait("Provider", provider);
    public static IEnumerable<TheoryDataRow<string, string>> EligibleCases =>
        from provider in Providers
        from boundary in Boundaries
        select new TheoryDataRow<string, string>(provider, boundary).WithTrait("Provider", provider);

    [Theory]
    [MemberData(nameof(RestrictedCases))]
    public async Task Authorization_observes_committed_restrictions_before_issuing_a_code(string provider, string boundary, string restriction)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        using var controlled = IssuerEnvironment.CreateBrowserClient(cookies, autoRedirect: false);
        var pending = await PrepareAsync(environment, browser, controlled, cookies, boundary);
        await using (var state = await RestrictionState.CaptureAsync(environment))
        {
            await state.CommitAsync(restriction, pending.Flow.ExpectedSessionId!);
            var before = await AuthorizationSnapshot.CaptureAsync(environment);
            using var response = await NavigateAsync(environment, controlled, pending.Target);
            await RequireDenialAsync(environment, controlled, response, pending, boundary, restriction);
            before.RequireDenied(await AuthorizationSnapshot.CaptureAsync(environment), allowAdmission: boundary == "new" && IsSessionRestriction(restriction));
            Assert.True(pending.Flow.CallbackCount == 0 && pending.Flow.ExchangeCount == 0 && string.IsNullOrEmpty(pending.Flow.Code),
                "The controlled denial must not reach the relying party or exchange a code.");
        }
        await RecoverAsync(environment);
    }

    [Theory]
    [MemberData(nameof(EligibleCases))]
    public async Task Unchanged_eligible_session_completes_each_authorization_boundary(string provider, string boundary)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        using var controlled = IssuerEnvironment.CreateBrowserClient(cookies, autoRedirect: false);
        var pending = await PrepareAsync(environment, browser, controlled, cookies, boundary);
        var before = await AuthorizationSnapshot.CaptureAsync(environment);
        using var response = await NavigateAsync(environment, controlled, pending.Target);
        var callback = RequireCallback(environment, response, pending.Flow);
        var query = QueryHelpers.ParseQuery(callback.Query);
        Assert.True(!string.IsNullOrEmpty(query["code"]) && !query.ContainsKey("error"), "The eligible boundary must return one code.");
        using var delivered = await browser.GetAsync(callback, TestContext.Current.CancellationToken);
        Assert.True(delivered.IsSuccessStatusCode && await pending.Flow.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken) && pending.Flow.Validated,
            "The eligible boundary must pass independent token, subject, nonce and original authentication-time validation.");
        Assert.True(pending.Flow.CallbackCount == 1 && pending.Flow.ExchangeCount == 1, "The positive control must exchange exactly once.");
        var after = await AuthorizationSnapshot.CaptureAsync(environment);
        Assert.Equal(before.Approvals + 1, after.Approvals);
        Assert.Equal(before.Authorizations.Count + 1, after.Authorizations.Count);
        if (pending.Flow.InteractionId is not null)
        {
            var interaction = await InteractionAsync(environment, pending.Flow.InteractionId);
            Assert.True(interaction.Consumed, "The successful cached flow must consume its own interaction.");
        }
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Completed_authorization_cannot_adopt_another_current_eligible_subject(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        using var controlled = IssuerEnvironment.CreateBrowserClient(cookies, autoRedirect: false);
        var pending = await PrepareAsync(environment, browser, controlled, cookies, "completed");
        var original = await InteractionAsync(environment, pending.Flow.InteractionId!);
        await using (var alternate = await TemporaryPilotOwner.CreateAsync(environment))
        {
            var otherCookies = new CookieContainer();
            using var otherBrowser = IssuerEnvironment.CreateBrowserClient(otherCookies);
            var other = await TestLogin.BeginAsync(environment, otherBrowser, exchange: true);
            await other.FollowAsync(environment, otherBrowser, await other.CompleteAsync(environment, otherBrowser, alternate.Username));
            Assert.True(other.Flow.Validated && alternate.UserId != original.CompletedUserId && other.Flow.ExpectedSessionId != original.CompletedSessionId,
                "Subject substitution control requires a distinct genuinely validated subject and session.");
            await RequireEligibleAsync(environment, other.Flow.ExpectedSessionId!, alternate.UserId);
            var actual = otherCookies.GetCookies(environment.Issuer)[PilotProfile.SessionCookie];
            Assert.True(actual is not null && actual.Secure && actual.HttpOnly, "The replacement must be a genuine protected application cookie.");
            var previous = cookies.GetCookies(environment.Issuer)[PilotProfile.SessionCookie];
            Assert.True(previous is not null && previous.Value != actual!.Value, "The replacement cookie must differ from the original subject's ticket.");
            cookies.Add(environment.Issuer, new Cookie(PilotProfile.SessionCookie, actual!.Value, "/") { Secure = true, HttpOnly = true });
            var presented = cookies.GetCookies(environment.Issuer).Cast<Cookie>().Where(value => value.Name == PilotProfile.SessionCookie).ToArray();
            Assert.True(presented.Length == 1 && presented[0].Value == actual.Value, "The tested authorization browser must actually present subject B's exact genuine ticket.");
            RequireCorrelation(environment, cookies, original);
            var before = await AuthorizationSnapshot.CaptureAsync(environment);
            using var response = await NavigateAsync(environment, controlled, pending.Target);
            await RequireDenialAsync(environment, controlled, response, pending, "completed", "session-revoked");
            before.RequireDenied(await AuthorizationSnapshot.CaptureAsync(environment), allowAdmission: false);
            await RequireEligibleAsync(environment, other.Flow.ExpectedSessionId!, alternate.UserId);
        }
        await RecoverAsync(environment);
    }

    private sealed record PendingAuthorization(TestFlow Flow, Uri Target);

    private static async Task<PendingAuthorization> PrepareAsync(IssuerEnvironment environment, HttpClient browser, HttpClient controlled, CookieContainer cookies, string boundary)
    {
        if (boundary == "completed")
        {
            var login = await TestLogin.BeginAsync(environment, browser, prompt: "login", exchange: true);
            var target = await login.CompleteAsync(environment, browser);
            var row = await InteractionAsync(environment, login.Interaction);
            Assert.True(!row.Consumed && row.CompletedSessionId == login.Flow.ExpectedSessionId,
                "Pause after this request's genuine fresh completion, before consumption.");
            RequireCorrelation(environment, cookies, row);
            await RequireEligibleAsync(environment, row.CompletedSessionId!, row.CompletedUserId!);
            return new(login.Flow, target);
        }
        // Establish the genuine session without a redundant warmup code exchange.
        // This setup interaction remains completed but unconsumed until fixture teardown.
        var established = await TestLogin.BeginAsync(environment, browser);
        await established.CompleteAsync(environment, browser);
        var authenticated = established.Flow;
        var completed = await InteractionAsync(environment, established.Interaction);
        await RequireEligibleAsync(environment, authenticated.ExpectedSessionId!, completed.CompletedUserId!);
        var flow = environment.RelyingParty.Prepare(exchange: true, prompt: boundary == "new" ? null : "none");
        await environment.RecordSessionAsync(browser, flow, authenticated.ExpectedSessionId!);
        using var started = await controlled.GetAsync(environment.RelyingParty.StartUri(flow), TestContext.Current.CancellationToken);
        var authorization = RequireIssuerRedirect(environment, started);
        Assert.True(authorization.AbsoluteUri == environment.RelyingParty.AuthorizationUri(flow), "The RP must preserve the original request including prompt.");
        if (boundary != "pending-none")
        {
            return new(flow, authorization);
        }
        using var cached = await controlled.GetAsync(authorization, TestContext.Current.CancellationToken);
        var cachedTarget = RequireIssuerRedirect(environment, cached);
        var requestUri = QueryHelpers.ParseQuery(cachedTarget.Query)["request_uri"].ToString();
        Assert.True(requestUri.Length > 0 && flow.Prompt == "none", "Pause only an originally silent cached request.");
        using var allocated = await controlled.GetAsync(cachedTarget, TestContext.Current.CancellationToken);
        var continuation = RequireIssuerRedirect(environment, allocated);
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var interaction = await database.Set<LoginInteraction>().AsNoTracking().SingleAsync(value => value.RequestUri == requestUri, TestContext.Current.CancellationToken);
        Assert.True(!interaction.Consumed && interaction.CompletedSessionId is null && interaction.CompletedUserId is null &&
            interaction.ExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 10 && continuation == new Uri(environment.Issuer, LoginInteractions.Resume(interaction)),
            "The exact native cached request must have a live pending, uncompleted interaction before restriction.");
        Assert.True(await database.Set<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken>().AnyAsync(value => value.Id == interaction.RequestTokenId &&
            value.Type == OpenIddict.Abstractions.OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken, TestContext.Current.CancellationToken),
            "The pending interaction must reference the persisted native request token.");
        flow.InteractionId = interaction.Id;
        RequireCorrelation(environment, cookies, interaction);
        var session = await database.Set<LocalIdentitySession>().AsNoTracking().SingleAsync(value => value.Id == flow.ExpectedSessionId, TestContext.Current.CancellationToken);
        await RequireEligibleAsync(environment, session.Id, session.UserId);
        return new(flow, continuation);
    }

    private static async Task<HttpResponseMessage> NavigateAsync(IssuerEnvironment environment, HttpClient controlled, Uri start)
    {
        var target = start;
        for (var hop = 0; hop < 5; hop++)
        {
            RequireIssuerAuthorization(environment, target);
            var response = await controlled.GetAsync(target, TestContext.Current.CancellationToken);
            try
            {
                Assert.True(!response.Headers.TryGetValues("Set-Cookie", out var cookies) ||
                    cookies.All(value => !value.StartsWith(PilotProfile.SessionCookie + "=", StringComparison.Ordinal)),
                    "Authorization navigation must not issue or replace the application cookie.");
                if (response.StatusCode != HttpStatusCode.Redirect || response.Headers.Location is not { } location)
                {
                    return response;
                }
                var next = new Uri(environment.Issuer, location);
                if (next.GetLeftPart(UriPartial.Path) == environment.RelyingParty.Callback ||
                    next.GetLeftPart(UriPartial.Authority) == environment.Issuer.GetLeftPart(UriPartial.Authority) && next.AbsolutePath == "/account/login")
                {
                    return response;
                }
                RequireIssuerAuthorization(environment, next);
                target = next;
            }
            catch
            {
                response.Dispose();
                throw;
            }
            response.Dispose();
        }
        throw new InvalidOperationException("Authorization exceeded the bounded redirect count.");
    }

    private static async Task RequireDenialAsync(IssuerEnvironment environment, HttpClient controlled, HttpResponseMessage response, PendingAuthorization pending, string boundary, string restriction)
    {
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        SafeAssertions.ContainsNoSensitiveMaterial(text + response.Headers, environment.Password, environment.ClientSecret, pending.Flow.Verifier);
        Assert.True(response.Headers.CacheControl?.NoStore == true, "Authorization responses must not be stored.");
        if (restriction == "permission")
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.True(response.Headers.Location is null, "Native permission rejection must not redirect.");
            Assert.True(response.Content.Headers.ContentType?.MediaType == "text/plain", "Native permission rejection must use text protocol errors.");
            var lines = text.Split('\n').Select(value => value.TrimEnd('\r')).ToArray();
            Assert.True(lines.Contains("error:invalid_request", StringComparer.Ordinal) && lines.Contains("error_uri:https://documentation.openiddict.com/errors/ID2051", StringComparer.Ordinal),
                "Removed scope permission must fail at native validation with ID2051.");
            SafeAssertions.ContainsNoSensitiveMaterial(text, pending.Flow.State, pending.Flow.Nonce);
            return;
        }
        if (boundary == "new" && IsSessionRestriction(restriction))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var target = new Uri(environment.Issuer, response.Headers.Location!);
            Assert.True(target.GetLeftPart(UriPartial.Authority) == environment.Issuer.GetLeftPart(UriPartial.Authority) && target.AbsolutePath == "/account/login" && string.IsNullOrEmpty(target.Fragment),
                "An ineligible ordinary session must require a protected login.");
            var loginQuery = QueryHelpers.ParseQuery(target.Query);
            Assert.True(loginQuery.Count == 1 && loginQuery.ContainsKey("interaction"), "Login redirect may contain only its bound interaction identifier.");
            var id = loginQuery["interaction"].ToString();
            var row = await InteractionAsync(environment, id);
            Assert.True(!row.Consumed && row.CompletedSessionId is null && row.CompletedUserId is null, "The new login must remain unauthenticated.");
            using var form = await controlled.GetAsync(target, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, form.StatusCode);
            Assert.True(form.Headers.Location is null, "Protected login must render locally.");
            var html = await form.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            SafeAssertions.ContainsNoSensitiveMaterial(html, environment.Password, environment.ClientSecret, pending.Flow.Verifier);
            Assert.True(IssuerEnvironment.InputValue(html, "Interaction") == id &&
                !string.IsNullOrEmpty(IssuerEnvironment.InputValue(html, "__RequestVerificationToken")),
                "The correlated login must render its exact interaction and genuine antiforgery form.");
            return;
        }
        var callback = RequireCallback(environment, response, pending.Flow);
        var query = QueryHelpers.ParseQuery(callback.Query);
        var expected = IsSessionRestriction(restriction) && boundary != "completed" ? "login_required" : "access_denied";
        Assert.True(query["error"] == expected && !query.ContainsKey("code") && !query.ContainsKey("access_token") && !query.ContainsKey("id_token"),
            "The authorization boundary must return its exact protocol denial without a code or tokens.");
    }

    private static bool IsSessionRestriction(string restriction) => restriction is "disabled" or "stamp" or "session-revoked" or "session-expired";
    private static Uri RequireCallback(IssuerEnvironment environment, HttpResponseMessage response, TestFlow flow)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var target = response.Headers.Location;
        Assert.True(target is { IsAbsoluteUri: true } && target.GetLeftPart(UriPartial.Path) == environment.RelyingParty.Callback && string.IsNullOrEmpty(target.Fragment),
            "The protocol callback must be the exact registered HTTPS destination.");
        Assert.True(QueryHelpers.ParseQuery(target!.Query)["state"] == flow.State, "The response must retain the original RP state.");
        return target;
    }
    private static Uri RequireIssuerRedirect(IssuerEnvironment environment, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var target = new Uri(environment.Issuer, response.Headers.Location);
        RequireIssuerAuthorization(environment, target);
        return target;
    }
    private static void RequireIssuerAuthorization(IssuerEnvironment environment, Uri target) =>
        Assert.True(target.GetLeftPart(UriPartial.Authority) == environment.Issuer.GetLeftPart(UriPartial.Authority) && target.AbsolutePath == "/connect/authorize" && string.IsNullOrEmpty(target.Fragment),
            "Only the canonical issuer authorization endpoint may be followed.");
    private static void RequireCorrelation(IssuerEnvironment environment, CookieContainer cookies, LoginInteraction row) =>
        Assert.True(InteractionSecrets.Matches(cookies.GetCookies(environment.Issuer)[PilotProfile.CorrelationPrefix + row.Id]?.Value, row.BrowserDigest),
            "The tested browser must retain this interaction's exact correlation proof.");
    private static async Task<LoginInteraction> InteractionAsync(IssuerEnvironment environment, string id)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LoginInteraction>().AsNoTracking().SingleAsync(value => value.Id == id, TestContext.Current.CancellationToken);
    }
    private static async Task RequireEligibleAsync(IssuerEnvironment environment, string sessionId, string userId)
    {
        await using var runtime = environment.RuntimeServices();
        await using var scope = runtime.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<PilotEligibility>().SessionAsync(sessionId, userId, TestContext.Current.CancellationToken) is not null,
            "The selected genuine session must be currently eligible before the controlled boundary.");
    }
    private static async Task RecoverAsync(IssuerEnvironment environment)
    {
        using var recovery = IssuerEnvironment.CreateBrowserClient();
        Assert.True((await environment.AuthorizeAsync(recovery, exchange: true)).Validated, "Recovery requires a fresh genuine login and independently validated exchange.");
    }
}
