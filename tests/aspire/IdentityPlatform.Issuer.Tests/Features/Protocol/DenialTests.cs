using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("issuer")]
public sealed class DenialTests(IssuerFixture fixture)
{
    private static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private static readonly (string Key, string? Value)[] InvalidRequests =
    [
        ("code_challenge", null), ("code_challenge_method", null), ("code_challenge_method", "plain"),
        ("code_challenge_method", "S512"), ("code_challenge", "short"), ("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB"),
        ("client_id", "unregistered-client"), ("redirect_uri", "https://127.0.0.1/unregistered"),
        ("response_type", "token"), ("response_mode", "fragment"), ("prompt", "consent"),
        ("prompt", "select_account"), ("max_age", "-1"), ("max_age", "bad"),
        ("scope", "openid offline_access"), ("scope", "openid extra"), ("scope", "openid"), ("scope", "pilot_api")
    ];
    private static readonly string[] ExchangeFailures = ["wrong-verifier", "missing-verifier", "wrong-secret", "wrong-client", "wrong-redirect"];
    public static IEnumerable<TheoryDataRow<string, string, string?>> Requests =>
        from provider in Providers
        from value in InvalidRequests
        select new TheoryDataRow<string, string, string?>(provider, value.Key, value.Value).WithTrait("Provider", provider);
    public static IEnumerable<TheoryDataRow<string, string>> Exchanges =>
        from provider in Providers
        from failure in ExchangeFailures
        select new TheoryDataRow<string, string>(provider, failure).WithTrait("Provider", provider)
            .WithTrait("CriticalDecision", failure == "wrong-verifier" ? "pkce-proof" : "none");

    [Theory]
    [MemberData(nameof(Requests))]
    public async Task Invalid_authorization_never_displays_login_or_issues_a_code(string provider, string key, string? value)
    {
        var environment = await fixture.GetAsync(provider);
        var flow = environment.RelyingParty.Prepare();
        flow.QueryOverrides[key] = value;
        using var browser = IssuerEnvironment.CreateBrowserClient();
        using var response = await browser.GetAsync(environment.RelyingParty.StartUri(flow), TestContext.Current.CancellationToken);
        Assert.NotEqual("/account/login", response.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest);
        Assert.True(string.IsNullOrEmpty(flow.Code), "A denied flow unexpectedly issued a code.");
        if (response.IsSuccessStatusCode)
        {
            Assert.False(await flow.Completion.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.NotEmpty(flow.Error);
            Assert.True(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken) == "Request denied", "Unexpected denial response.");
        }
    }

    [Theory]
    [MemberData(nameof(Exchanges))]
    public async Task Code_exchange_rejects_incorrect_proof_and_client_bindings(string provider, string failure)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var flow = await environment.AuthorizeAsync(browser);
        using var response = await environment.RelyingParty.ExchangeAsync(flow.Code,
            failure == "wrong-verifier" ? new string('x', 43) : failure == "missing-verifier" ? "" : flow.Verifier,
            clientSecret: failure == "wrong-secret" ? "invalid-synthetic-secret" : null,
            redirect: failure == "wrong-redirect" ? environment.RelyingParty.Callback + "/changed" : null,
            clientId: failure == "wrong-client" ? "unknown-client" : "identityplatform-pilot",
            cancellationToken: TestContext.Current.CancellationToken);
        if (failure == "wrong-verifier")
        {
            await CriticalDecisionAssertions.DeniedTokensAsync(response,
                async tokens => string.IsNullOrEmpty(await environment.RelyingParty.ValidateAsync(tokens, flow, TestContext.Current.CancellationToken)),
                "Critical decision bypass: pkce-proof.");
        }
        Assert.Equal(failure is "wrong-secret" or "wrong-client" ? HttpStatusCode.Unauthorized : HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(body.RootElement.TryGetProperty("error", out _));
        Assert.False(body.RootElement.TryGetProperty("access_token", out _));
        Assert.False(body.RootElement.TryGetProperty("id_token", out _));
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Maximum_length_unreserved_verifier_succeeds(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var prepared = environment.RelyingParty.Prepare(exchange: true, verifier: new string('~', 128));
        Assert.True((await environment.AuthorizeAsync(browser, prepared: prepared)).Validated);
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    [Trait("CriticalDecision", "pkce-syntax")]
    public async Task Matching_sha256_does_not_allow_a_short_verifier(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        using var browser = IssuerEnvironment.CreateBrowserClient();
        var prepared = environment.RelyingParty.Prepare();
        prepared.QueryOverrides["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes("x")));
        var flow = await environment.AuthorizeAsync(browser, prepared: prepared);
        using var response = await environment.RelyingParty.ExchangeAsync(flow.Code, "x", cancellationToken: TestContext.Current.CancellationToken);
        await CriticalDecisionAssertions.DeniedTokensAsync(response,
            async tokens => string.IsNullOrEmpty(await environment.RelyingParty.ValidateAsync(tokens, flow, TestContext.Current.CancellationToken)),
            "Critical decision bypass: pkce-syntax.");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("invalid_grant", body.RootElement.GetProperty("error").GetString());
        Assert.False(body.RootElement.TryGetProperty("access_token", out _));
    }

    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Silent_request_without_session_returns_login_required_without_ui(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var flow = environment.RelyingParty.Prepare(prompt: "none");
        using var browser = IssuerEnvironment.CreateBrowserClient();
        using var response = await browser.GetAsync(environment.RelyingParty.StartUri(flow), TestContext.Current.CancellationToken);
        Assert.Equal("/callback", response.RequestMessage!.RequestUri!.AbsolutePath);
        Assert.Equal("login_required", flow.Error);
        Assert.True(string.IsNullOrEmpty(flow.Code), "A denied flow unexpectedly issued a code.");
        Assert.False(await flow.Completion.Task);
    }
}
