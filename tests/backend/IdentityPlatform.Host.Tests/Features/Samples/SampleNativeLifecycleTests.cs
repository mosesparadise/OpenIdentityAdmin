using System.Net;

namespace IdentityPlatform.Host.Tests.Features.Samples;

/// <summary>Proves native trust and ephemeral browser authority independently of the waiting setup screen.</summary>
public sealed class SampleNativeLifecycleTests
{
    private const string Home = "/";
    private const string Login = "/login";
    private const string CookieHeader = "Cookie";
    private const string SetCookieHeader = "Set-Cookie";
    private const string SessionPrefix = "__Host-sample-session=";
    private const string KeyEndpoint = "connect/jwks";
    private const string Discovery = "/.well-known/openid-configuration";
    private const string SignedIn = "Signed in as";

    /// <summary>After real native sign-in, a fully configured new process rejects the old protected cookie and callback.</summary>
    [Fact]
    public async Task Given_PreviousRunCookie_When_FreshConfiguredHostReceivesIt_Then_NoAuthorityIsRestored()
    {
        using var issuer = new SampleNativeIssuer();
        string cookie;
        string callback;
        await using (var original = new SampleNativeClient(issuer))
        {
            await original.App.StartAsync(TestContext.Current.CancellationToken);
            await original.ConfigureAsync();
            callback = await SampleNativeSignInTests.ChallengeAsync(original, issuer);
            using var response = await original.SendAsync(HttpMethod.Get, callback);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            cookie = Assert.Single(response.Headers.GetValues(SetCookieHeader), value => value.StartsWith(SessionPrefix, StringComparison.Ordinal)).Split(';')[0];
        }
        await using var fresh = new SampleNativeClient(issuer);
        await fresh.App.StartAsync(TestContext.Current.CancellationToken);
        await fresh.ConfigureAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, Home);
        request.Headers.Add(CookieHeader, cookie);
        using var home = await fresh.Client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        var body = await home.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Sign in", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Configure this sample", body, StringComparison.Ordinal);
        Assert.DoesNotContain(SignedIn, body, StringComparison.Ordinal);
        using var replay = await fresh.SendAsync(HttpMethod.Get, callback);
        Assert.NotEqual(HttpStatusCode.Redirect, replay.StatusCode);
        Assert.Equal(1, issuer.TokenCalls);
    }

    /// <summary>Unexpected key metadata is rejected before any key request or credential-bearing token exchange.</summary>
    [Fact]
    public async Task Given_SubstitutedKeyEndpoint_When_NativeChallengeLoadsMetadata_Then_NoUntrustedRequestIsSent()
    {
        using var issuer = new SampleNativeIssuer { Fault = KeyEndpoint };
        await using var browser = new SampleNativeClient(issuer);
        await browser.App.StartAsync(TestContext.Current.CancellationToken);
        await browser.ConfigureAsync();
        using var response = await browser.PostAsync(Login);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var cookies = response.Headers.TryGetValues(SetCookieHeader, out var values) ? values : [];
        Assert.All(cookies.Where(value => value.StartsWith(SessionPrefix, StringComparison.Ordinal)), value => Assert.StartsWith(SessionPrefix + ";", value, StringComparison.Ordinal));
        Assert.Equal(1, issuer.Calls[Discovery]);
        Assert.Single(issuer.Calls);
        Assert.Equal(0, issuer.TokenCalls);
        using var home = await browser.SendAsync(HttpMethod.Get, Home);
        Assert.DoesNotContain(SignedIn, await home.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }
}
