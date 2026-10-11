using System.Net;
using System.Security.Cryptography;
using IdentityPlatform.AuthServer.Features.Accounts.Login;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("interaction-acceptance")]
public sealed class RequestBindingTests(IssuerFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly string[] Methods = ["GET", "POST"];
    private static readonly string[] Overlays = ["redirect_uri", "scope", "state", "nonce", "code_challenge", "code_challenge_method"];
    private static readonly string[] Freshness = ["prompt-login", "max-age-zero", "max-age-stale"];
    private static readonly string[] ClientBindings = ["missing", "mismatched"];
    private static readonly bool[] Flags = [false, true];
    public static IEnumerable<TheoryDataRow<string, string, string>> OverlayCases => Rows(Overlays);
    public static IEnumerable<TheoryDataRow<string, string, string>> FreshnessCases => Rows(Freshness);
    public static IEnumerable<TheoryDataRow<string, string, string>> ClientCases => Rows(ClientBindings);
    public static IEnumerable<TheoryDataRow<string, bool, bool>> DelayCases =>
        from provider in Providers
        from prompt in Flags
        from expired in Flags
        select new TheoryDataRow<string, bool, bool>(provider, prompt, expired).WithTrait("Provider", provider);
    public static IEnumerable<TheoryDataRow<string, bool>> CorrelationCases =>
        from provider in Providers
        from wrong in Flags
        select new TheoryDataRow<string, bool>(provider, wrong).WithTrait("Provider", provider)
            .WithTrait("CriticalDecision", wrong ? "browser-binding" : "none");

    private static IEnumerable<TheoryDataRow<string, string, string>> Rows(string[] cases) =>
        from provider in Providers
        from method in Methods
        from value in cases
        select new TheoryDataRow<string, string, string>(provider, method, value).WithTrait("Provider", provider);

    [Theory]
    [MemberData(nameof(OverlayCases))]
    public async Task Cached_request_ignores_outer_parameter_overlays(string provider, string method, string parameter)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        using var controlled = IssuerEnvironment.CreateBrowserClient(cookies, autoRedirect: false);
        var login = await TestLogin.BeginAsync(environment, browser, exchange: true);
        var continuation = await login.CompleteAsync(environment, browser);
        var values = Parameters(continuation);
        values[parameter] = parameter switch
        {
            "redirect_uri" => "https://localhost:1/unapproved-callback",
            "scope" => "openid unapproved_scope",
            "code_challenge_method" => "plain",
            "code_challenge" => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)),
            _ => "outer-parameter-must-not-win"
        };
        var approvals = await ApprovalCountAsync(environment);
        using var response = await ResumeAsync(environment, controlled, method, values);
        var callback = Callback(environment, response);
        var query = QueryHelpers.ParseQuery(callback.Query);
        Assert.True(query["state"] == login.Flow.State && !string.IsNullOrEmpty(query["code"]), "Original callback state/code binding was lost.");
        await DeliverAsync(browser, callback, login);
        Assert.Equal(approvals + 1, await ApprovalCountAsync(environment));
        Assert.True((await InteractionAsync(environment, login)).Consumed);
    }

    [Theory]
    [MemberData(nameof(FreshnessCases))]
    public async Task Cached_request_freshness_cannot_be_removed_before_password_completion(string provider, string method, string mode)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        using var controlled = IssuerEnvironment.CreateBrowserClient(cookies, autoRedirect: false);
        var original = await environment.AuthorizeAsync(browser, exchange: true);
        await AgeSessionAsync(environment, original.ExpectedSessionId!, 120);
        var login = await TestLogin.BeginAsync(environment, browser, prompt: mode == "prompt-login" ? "login" : null,
            maximumAge: mode == "max-age-zero" ? "0" : mode == "max-age-stale" ? "60" : null, exchange: true);
        var row = await InteractionAsync(environment, login);
        var values = Parameters(new Uri(environment.Issuer, LoginInteractions.Resume(row)));
        values["prompt"] = "none";
        values["max_age"] = "999999";
        var approvals = await ApprovalCountAsync(environment);
        using (var response = await ResumeAsync(environment, controlled, method, values))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var target = new Uri(environment.Issuer, response.Headers.Location!);
            Assert.True(target.GetLeftPart(UriPartial.Authority) == environment.Issuer.GetLeftPart(UriPartial.Authority) &&
                target.AbsolutePath == "/account/login" && QueryHelpers.ParseQuery(target.Query)["interaction"] == login.Interaction,
                "Cached freshness must still require the original bound form.");
        }
        Assert.Equal(approvals, await ApprovalCountAsync(environment));
        Assert.True(string.IsNullOrEmpty(login.Flow.Code), "Freshness overlay issued a code without password completion.");
        await login.FollowAsync(environment, browser, await login.CompleteAsync(environment, browser));
        Assert.True(login.Flow.ExpectedSessionId != original.ExpectedSessionId, "Freshness must create a new session.");
    }

    [Theory]
    [MemberData(nameof(ClientCases))]
    public async Task Cached_request_requires_the_original_outer_client_binding(string provider, string method, string change)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        using var controlled = IssuerEnvironment.CreateBrowserClient(cookies, autoRedirect: false);
        var login = await TestLogin.BeginAsync(environment, browser, exchange: true);
        var continuation = await login.CompleteAsync(environment, browser);
        var values = Parameters(continuation);
        if (change == "missing")
        {
            values.Remove("client_id");
        }
        else
        {
            values["client_id"] = "mismatched-client";
        }
        var approvals = await ApprovalCountAsync(environment);
        using (var response = await ResumeAsync(environment, controlled, method, values))
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Null(response.Headers.Location);
            Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            Assert.True(body.Split('\n').Any(line => line.TrimEnd('\r') == "error:invalid_request"), "Expected native cached-client binding rejection.");
        }
        Assert.Equal(approvals, await ApprovalCountAsync(environment));
        Assert.False((await InteractionAsync(environment, login)).Consumed);
        // A rejected outer binding must not consume the legitimate original handle.
        await login.FollowAsync(environment, browser, continuation);
    }

    [Theory]
    [MemberData(nameof(DelayCases))]
    public async Task Positive_max_age_is_rechecked_after_password_completion(string provider, bool promptLogin, bool aged)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var login = await TestLogin.BeginAsync(environment, browser, prompt: promptLogin ? "login" : null, maximumAge: "60", exchange: true);
        var continuation = await login.CompleteAsync(environment, browser);
        if (aged)
        {
            await AgeSessionAsync(environment, login.Flow.ExpectedSessionId!, 120);
        }
        var approvals = await ApprovalCountAsync(environment);
        if (aged)
        {
            using var response = await browser.GetAsync(continuation, TestContext.Current.CancellationToken);
            Assert.Equal("login_required", login.Flow.Error);
            Assert.True(string.IsNullOrEmpty(login.Flow.Code), "Stale completed authentication issued a code.");
            Assert.False((await InteractionAsync(environment, login)).Consumed);
            Assert.Equal(approvals, await ApprovalCountAsync(environment));
        }
        else
        {
            await login.FollowAsync(environment, browser, continuation);
            Assert.True((await InteractionAsync(environment, login)).Consumed);
            Assert.Equal(approvals + 1, await ApprovalCountAsync(environment));
        }
    }

    [Theory]
    [MemberData(nameof(CorrelationCases))]
    public async Task Another_browsers_valid_antiforgery_cannot_replace_correlation_binding(string provider, bool wrongCookie)
    {
        var environment = await fixture.GetAsync(provider);
        using var firstBrowser = IssuerEnvironment.CreateBrowserClient();
        var cookies = new CookieContainer();
        using var secondBrowser = IssuerEnvironment.CreateBrowserClient(cookies);
        var first = await TestLogin.BeginAsync(environment, firstBrowser);
        var second = await TestLogin.BeginAsync(environment, secondBrowser, exchange: true);
        if (wrongCookie)
        {
            cookies.Add(environment.Issuer, new Cookie(PilotProfile.CorrelationPrefix + first.Interaction,
                WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)), "/")
            {
                Secure = true,
                HttpOnly = true
            });
        }
        var before = await SessionCountAsync(environment);
        using (var denied = await first.SubmitAsync(environment, secondBrowser, antiforgery: second.Antiforgery))
        {
            if (denied.StatusCode == HttpStatusCode.OK)
            {
                var completed = await InteractionAsync(environment, first);
                Assert.True(completed.CompletedSessionId is not null && completed.CompletedUserId is not null && !completed.Consumed &&
                    await SessionCountAsync(environment) == before + 1 &&
                    cookies.GetCookies(environment.Issuer).Cast<Cookie>().Any(value => value.Name == PilotProfile.SessionCookie),
                    "Unexpected form success lacked foreign-interaction completion and session evidence.");
                Assert.Fail("Critical decision bypass: browser-binding.");
            }
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        }
        var row = await InteractionAsync(environment, first);
        Assert.True(row.CompletedSessionId is null && !row.Consumed, "Correlation failure changed the other interaction.");
        Assert.Equal(before, await SessionCountAsync(environment));
        Assert.False(cookies.GetCookies(environment.Issuer).Cast<Cookie>().Any(value => value.Name == PilotProfile.SessionCookie), "A rejected binding must not issue an authentication cookie.");
        Assert.True(string.IsNullOrEmpty(first.Flow.Code), "Correlation failure issued a code.");
        // The same antiforgery token succeeds for the second browser's own interaction.
        await second.FollowAsync(environment, secondBrowser, await second.CompleteAsync(environment, secondBrowser));
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Authenticated_tabs_complete_distinct_original_flows_without_losing_other_correlation(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        await environment.AuthorizeAsync(browser, exchange: true);
        var first = await TestLogin.BeginAsync(environment, browser, prompt: "login", exchange: true);
        var second = await TestLogin.BeginAsync(environment, browser, prompt: "login", exchange: true);
        Assert.True(first.Interaction != second.Interaction && first.Flow.State != second.Flow.State && first.Flow.Nonce != second.Flow.Nonce,
            "Independent tabs must retain independent request bindings.");
        await first.FollowAsync(environment, browser, await first.CompleteAsync(environment, browser));
        Assert.False(cookies.GetCookies(environment.Issuer).Cast<Cookie>().Any(value => value.Name == PilotProfile.CorrelationPrefix + first.Interaction), "The completed tab must clear its own correlation cookie.");
        Assert.True(cookies.GetCookies(environment.Issuer).Cast<Cookie>().Any(value => value.Name == PilotProfile.CorrelationPrefix + second.Interaction), "Completing one tab must preserve the other correlation cookie.");
        await second.FollowAsync(environment, browser, await second.CompleteAsync(environment, browser));
        Assert.True((await InteractionAsync(environment, first)).Consumed && (await InteractionAsync(environment, second)).Consumed);
        Assert.True(first.Flow.ExpectedSessionId != second.Flow.ExpectedSessionId, "Each fresh login must create its own session.");
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Direct_resume_cannot_adopt_a_replacement_tabs_session(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var first = await TestLogin.BeginAsync(environment, browser, prompt: "login");
        var original = await first.CompleteAsync(environment, browser);
        var second = await TestLogin.BeginAsync(environment, browser, prompt: "login", exchange: true);
        await second.FollowAsync(environment, browser, await second.CompleteAsync(environment, browser));
        var approvals = await ApprovalCountAsync(environment);
        using var response = await browser.GetAsync(original, TestContext.Current.CancellationToken);
        Assert.Equal("access_denied", first.Flow.Error);
        Assert.True(string.IsNullOrEmpty(first.Flow.Code), "A replaced completion adopted another session.");
        Assert.False((await InteractionAsync(environment, first)).Consumed);
        Assert.Equal(approvals, await ApprovalCountAsync(environment));
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Concurrent_http_resume_emits_one_code_before_the_relying_party_observes_it(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        using var controlled = IssuerEnvironment.CreateBrowserClient(cookies, autoRedirect: false);
        var login = await TestLogin.BeginAsync(environment, browser, exchange: true);
        var continuation = await login.CompleteAsync(environment, browser);
        var approvals = await ApprovalCountAsync(environment);
        // Freeze each request's cookies before a response can delete the shared correlation cookie.
        var cookieHeader = cookies.GetCookieHeader(environment.Issuer);
        using var raw = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
        async Task<HttpResponseMessage> Resume()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, continuation);
            request.Headers.Add("Cookie", cookieHeader);
            return await raw.SendAsync(request, TestContext.Current.CancellationToken);
        }
        var results = await Task.WhenAll(Resume(), Resume());
        using var first = results[0];
        using var second = results[1];
        var winners = results.Where(response => response.StatusCode == HttpStatusCode.Redirect &&
            !string.IsNullOrEmpty(QueryHelpers.ParseQuery(Callback(environment, response).Query).GetValueOrDefault("code"))).ToArray();
        Assert.True(winners.Length == 1, "Exactly one issuer response may contain a code.");
        foreach (var response in results.Where(response => !ReferenceEquals(response, winners[0])))
        {
            if (response.StatusCode == HttpStatusCode.Redirect)
            {
                var query = QueryHelpers.ParseQuery(Callback(environment, response).Query);
                Assert.True(query.GetValueOrDefault("error") == "access_denied" && query.GetValueOrDefault("state") == login.Flow.State &&
                    string.IsNullOrEmpty(query.GetValueOrDefault("code")), "The application-level losing resume must be denied.");
            }
            else
            {
                // A later arrival can encounter native request-token redemption before application handling.
                await AssertRedeemedRequestAsync(response);
            }
        }
        Assert.Equal(approvals + 1, await ApprovalCountAsync(environment));
        Assert.False(login.Flow.Completion.Task.IsCompleted, "The RP must not hide duplicate issuer responses.");
        await DeliverAsync(browser, Callback(environment, winners[0]), login);
        using var replay = await controlled.GetAsync(continuation, TestContext.Current.CancellationToken);
        await AssertRedeemedRequestAsync(replay);
        Assert.Equal(approvals + 1, await ApprovalCountAsync(environment));
    }

    private static async Task AssertRedeemedRequestAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        var lines = (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        Assert.True(lines.Contains("error:invalid_token", StringComparer.Ordinal) && !lines.Any(line => line.StartsWith("code:", StringComparison.Ordinal)),
            "Native redeemed request tokens must be rejected locally without a code.");
    }

    private static Dictionary<string, string> Parameters(Uri uri) => QueryHelpers.ParseQuery(uri.Query).ToDictionary(value => value.Key, value => value.Value.ToString(), StringComparer.Ordinal);

    private static async Task<HttpResponseMessage> ResumeAsync(IssuerEnvironment environment, HttpClient browser, string method, Dictionary<string, string> values)
    {
        if (method == "GET")
        {
            return await browser.GetAsync(new Uri(environment.Issuer, "/connect/authorize" + QueryString.Create(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value)))), TestContext.Current.CancellationToken);
        }
        using var form = new FormUrlEncodedContent(values);
        return await browser.PostAsync(new Uri(environment.Issuer, "/connect/authorize"), form, TestContext.Current.CancellationToken);
    }

    private static Uri Callback(IssuerEnvironment environment, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var uri = response.Headers.Location;
        Assert.True(uri is { IsAbsoluteUri: true } && uri.GetLeftPart(UriPartial.Path) == environment.RelyingParty.Callback,
            "Issuer response must target the exact registered callback.");
        return uri!;
    }

    private static async Task DeliverAsync(HttpClient browser, Uri callback, TestLogin login)
    {
        using var response = await browser.GetAsync(callback, TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode && await login.Flow.Completion.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken) && login.Flow.Validated,
            "Original state, nonce, proof, subject and claims must pass independent validation.");
    }

    private static async Task<LoginInteraction> InteractionAsync(IssuerEnvironment environment, TestLogin login)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LoginInteraction>().AsNoTracking()
            .SingleAsync(value => value.Id == login.Interaction, TestContext.Current.CancellationToken);
    }

    private static async Task AgeSessionAsync(IssuerEnvironment environment, string sessionId, int seconds)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var count = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LocalIdentitySession>().Where(value => value.Id == sessionId)
            .ExecuteUpdateAsync(update => update.SetProperty(value => value.AuthenticatedAt, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - seconds), TestContext.Current.CancellationToken);
        Assert.Equal(1, count);
    }

    private static async Task<int> ApprovalCountAsync(IssuerEnvironment environment)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<SecurityAuditIntent>()
            .CountAsync(value => value.Event == "authorization-decision", TestContext.Current.CancellationToken);
    }

    private static async Task<int> SessionCountAsync(IssuerEnvironment environment)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<LocalIdentitySession>().CountAsync(TestContext.Current.CancellationToken);
    }
}
