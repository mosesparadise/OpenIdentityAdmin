using System.Net;
using Microsoft.AspNetCore.WebUtilities;

namespace IdentityPlatform.Host.Tests.Features.Samples;

/// <summary>Proves real sample middleware, reference state, S256, token validation and server-held native cookies as one HTTP journey.</summary>
public sealed class SampleNativeSignInTests
{
    private const string HealthPath = "/health";
    private const string HomePath = "/";
    private const string LogoutPath = "/logout";
    private const string LogoutText = "logout";
    private const string LoginPath = "/login";
    private const string AuthorizationEndpoint = "connect/authorize";
    private const string FormActionSelfHttpsIssuerDevLocalhost17443Text = "form-action 'self' https://issuer.dev.localhost:17443;";
    private const string ContentSecurityPolicyHeader = "Content-Security-Policy";
    private const string CodeParameter = "code";
    private const string ResponseTypeParameter = "response_type";
    private const string S256Text = "S256";
    private const string ChallengeMethodParameter = "code_challenge_method";
    private const string CallbackText = "callback";
    private const string RedirectParameter = "redirect_uri";
    private const string StateParameter = "state";
    private const int IssuerCorrelationLimit = 512;
    private const string NonceParameter = "nonce";
    private const string ChallengeParameter = "code_challenge";
    private const string CallbackPath = "/callback";
    private const string SyntheticCodeText = "synthetic-code";
    private const string TokenEndpoint = "/connect/token";
    private const string IssuerParameter = "iss";

    /// <summary>Waiting and process health do not initialize native options; setup later enables a complete sign-in and terminal logout.</summary>
    [Fact]
    public async Task Given_WaitingHost_When_ConfiguredAndNativeCallbackCompletes_Then_SessionIsPrivateAndLogoutIsTerminal()
    {
        using var issuer = new SampleNativeIssuer();
        await using var browser = new SampleNativeClient(issuer);
        await browser.App.StartAsync(TestContext.Current.CancellationToken);
        using (var health = await browser.SendAsync(HttpMethod.Get, HealthPath))
        {
            Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        }
        using (var waiting = await browser.SendAsync(HttpMethod.Get, HomePath))
        {
            Assert.Contains("Configure this sample", await waiting.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }
        await browser.ConfigureAsync();
        var callback = await ChallengeAsync(browser, issuer);
        using var signedIn = await browser.SendAsync(HttpMethod.Get, callback);
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Equal("/", signedIn.Headers.Location!.OriginalString);
        Assert.Equal(1, issuer.TokenCalls);
        Assert.Equal(0, issuer.UserInfoCalls);
        using var home = await browser.SendAsync(HttpMethod.Get, HomePath);
        var body = await home.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("synthetic-subject", body, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-server-only-access-token", body, StringComparison.Ordinal);
        Assert.DoesNotContain("synthetic-secret", body, StringComparison.Ordinal);
        using var replay = await browser.SendAsync(HttpMethod.Get, callback);
        Assert.NotEqual(HttpStatusCode.Redirect, replay.StatusCode);
        Assert.Equal(1, issuer.TokenCalls);
        using var logout = await browser.PostAsync(LogoutPath);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        using var ended = await browser.SendAsync(HttpMethod.Get, HomePath);
        Assert.DoesNotContain("Signed in as", await ended.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    /// <summary>A cancelled browser attempt or invalid native nonce cannot create cookie authority after code redemption.</summary>
    [Theory]
    [InlineData("logout")]
    [InlineData("nonce")]
    public async Task Given_PendingChallenge_When_LogoutOrBadNonceOccurs_Then_CallbackCannotPublishSession(string fault)
    {
        using var issuer = new SampleNativeIssuer { Fault = fault };
        await using var browser = new SampleNativeClient(issuer);
        await browser.App.StartAsync(TestContext.Current.CancellationToken);
        await browser.ConfigureAsync();
        var callback = await ChallengeAsync(browser, issuer);
        if (fault == LogoutText)
        {
            using var logout = await browser.PostAsync(LogoutPath);
            Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        }
        using var response = await browser.SendAsync(HttpMethod.Get, callback);
        Assert.Equal(fault == LogoutText ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
        NoSessionCookie(response);
        Assert.Equal(1, issuer.TokenCalls);
        using var home = await browser.SendAsync(HttpMethod.Get, HomePath);
        Assert.DoesNotContain("Signed in as", await home.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    /// <summary>Native transport must reject encoded discovery, JWKS and token bodies before decompression or authentication.</summary>
    [Theory]
    [InlineData("/.well-known/openid-configuration")]
    [InlineData("/connect/jwks")]
    [InlineData("/connect/token")]
    public async Task Given_EncodedNativeResponse_When_SignInRuns_Then_NoSessionIsPublished(string path)
    {
        using var issuer = new SampleNativeIssuer { HostileResponse = path };
        await using var browser = new SampleNativeClient(issuer);
        await browser.App.StartAsync(TestContext.Current.CancellationToken);
        await browser.ConfigureAsync();
        using var challenge = await browser.PostAsync(LoginPath);
        if (challenge.Headers.Location is { } location && location.AbsoluteUri.StartsWith(SampleNativeIssuer.Issuer, StringComparison.Ordinal))
        {
            var callback = Callback(issuer, location);
            using var response = await browser.SendAsync(HttpMethod.Get, callback);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            NoSessionCookie(response);
        }
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest, challenge.StatusCode);
            NoSessionCookie(challenge);
        }
        Assert.Equal(1, issuer.Calls[path]);
        Assert.Equal(path == TokenEndpoint ? 1 : 0, issuer.TokenCalls);
        using var home = await browser.SendAsync(HttpMethod.Get, HomePath);
        Assert.DoesNotContain("Signed in as", await home.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    /// <summary>Permits a native deletion cookie but rejects every positive session value on failed authentication.</summary>
    private static void NoSessionCookie(HttpResponseMessage response)
    {
        const string header = "Set-Cookie";
        const string prefix = "__Host-sample-session=";
        const string deletion = "__Host-sample-session=;";
        var cookies = response.Headers.TryGetValues(header, out var values) ? values : [];
        Assert.All(cookies.Where(value => value.StartsWith(prefix, StringComparison.Ordinal)), value => Assert.StartsWith(deletion, value, StringComparison.Ordinal));
    }

    /// <summary>Captures only synthetic native authorization values and requires compact reference state and exact fixed request profile.</summary>
    internal static async Task<string> ChallengeAsync(SampleNativeClient browser, SampleNativeIssuer issuer)
    {
        using var response = await browser.PostAsync(LoginPath);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal(SampleNativeIssuer.Issuer + AuthorizationEndpoint, location.GetLeftPart(UriPartial.Path));
        Assert.Contains(FormActionSelfHttpsIssuerDevLocalhost17443Text, response.Headers.GetValues(ContentSecurityPolicyHeader).Single(), StringComparison.Ordinal);
        return Callback(issuer, location);
    }

    /// <summary>Forms a standard successful issuer query response using the genuine native state and S256 request.</summary>
    private static string Callback(SampleNativeIssuer issuer, Uri location)
    {
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal(CodeParameter, query[ResponseTypeParameter].ToString());
        Assert.Equal(S256Text, query[ChallengeMethodParameter].ToString());
        Assert.Equal(SampleNativeIssuer.Client + CallbackText, query[RedirectParameter].ToString());
        Assert.InRange(query[StateParameter].ToString().Length, 1, IssuerCorrelationLimit);
        issuer.Nonce = query[NonceParameter].ToString();
        issuer.Challenge = query[ChallengeParameter].ToString();
        return QueryHelpers.AddQueryString(CallbackPath, new Dictionary<string, string?> { [CodeParameter] = SyntheticCodeText, [StateParameter] = query[StateParameter].ToString(), [IssuerParameter] = SampleNativeIssuer.Issuer });
    }
}
