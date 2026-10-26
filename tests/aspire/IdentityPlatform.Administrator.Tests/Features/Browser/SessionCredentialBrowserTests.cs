using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Proves that actual issuer cookies and genuine OAuth bearer credentials do not act as BFF browser authority.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class SessionCredentialBrowserTests(BootstrapFixture fixture)
{
    private const string IssuerCookie = "__Host-identity-administrator";
    private const string Authorization = "Authorization";
    private const string Bearer = "Bearer ";
    private const int AccessFromEnd = 2;

    /// <summary>An independent browser carries the real same-host issuer cookie and then actual access token, yet only the original BFF cookie pair authenticates.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_ActualIssuerAndBearerCredentials_When_IndependentBrowserReadsBffStatus_Then_NeitherReplacesItsCookieAuthority(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            var capture = new BrowserNativeSecrets();
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, capture.Configure);
            await using var original = await BrowserSession.CreateAsync();
            var page = await original.Context.NewPageAsync();
            await BrowserHttpsJourney.SignInAsync(environment, page);
            var before = await BrowserHttpsState.SnapshotAsync(environment);
            var cookies = (await original.Context.CookiesAsync()).Where(cookie => cookie.Name == IssuerCookie).ToArray();
            var count = cookies.Length;
            Assert.Equal(1, count);
            Assert.True(environment.Profile.Issuer.Host == environment.Profile.ClientUri.Host);
            await using var foreign = await BrowserSession.CreateAsync();
            await foreign.Context.AddCookiesAsync([Copy(cookies[0])]);
            var other = await foreign.Context.NewPageAsync();
            await BrowserHttpsJourney.RequireStatusAsync(environment, other, false);
            var credentials = capture.Require();
            await foreign.Context.SetExtraHTTPHeadersAsync(new Dictionary<string, string> { [Authorization] = Bearer + credentials[^AccessFromEnd] });
            await BrowserHttpsJourney.RequireStatusAsync(environment, other, false);
            Assert.Equal(1, environment.Status.Bearer);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, true);
            Assert.Equal(1, environment.TokenRequests);
        });

    /// <summary>Retains the exact browser-issued cookie privately without creating or weakening its host/security attributes.</summary>
    private static Cookie Copy(BrowserContextCookiesResult cookie) => new()
    {
        Name = cookie.Name,
        Value = cookie.Value,
        Domain = cookie.Domain,
        Path = cookie.Path,
        Secure = cookie.Secure,
        HttpOnly = cookie.HttpOnly,
        SameSite = cookie.SameSite,
        Expires = cookie.Expires
    };
}
