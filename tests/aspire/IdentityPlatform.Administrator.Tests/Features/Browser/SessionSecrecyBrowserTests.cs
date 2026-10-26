using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Scans actual browser traffic and both origins for the real server-only credentials issued through unchanged trusted TLS.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class SessionSecrecyBrowserTests(BootstrapFixture fixture)
{
    /// <summary>Every actual assertion, verifier and issued OAuth token stays outside browser traffic, DOM and all application-readable storage.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_GenuineNativeBrowserLogin_When_ActualCredentialsAreComparedWithBothOrigins_Then_NoServerOnlySecretIsExposed(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            var capture = new BrowserNativeSecrets();
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, capture.Configure, distinctSites: true);
            await BrowserSecrecyControls.RunAsync(environment);
            await using var session = await BrowserSession.CreateAsync();
            await BrowserTrafficLifetime.RunAsync(session.Context, async traffic =>
            {
                var page = await session.Context.NewPageAsync();
                await BrowserHttpsJourney.SignInAsync(environment, page);
                var secrets = capture.Require();
                var cookies = await BrowserHttpsJourney.CookiesAsync(environment, page);
                var storageSecrets = secrets.Concat(cookies.Select(cookie => cookie.Value)).Concat(await traffic.FlowAsync()).ToArray();
                await page.GotoAsync(new Uri(environment.Profile.ClientUri, BrowserHttpsJourney.Login).AbsoluteUri);
                await BrowserSecretOracle.StorageAsync(page, storageSecrets);
                await page.GotoAsync(new Uri(environment.Profile.Issuer, AdministratorJourney.Security).AbsoluteUri);
                var issuerCookies = await session.Context.CookiesAsync();
                storageSecrets = storageSecrets.Concat(issuerCookies.Where(cookie => cookie.HttpOnly).Select(cookie => cookie.Value)).ToArray();
                await BrowserSecretOracle.StorageAsync(page, storageSecrets);
                await traffic.RequireAsync(secrets);
                Assert.Equal(1, environment.ParRequests);
                Assert.Equal(1, environment.TokenRequests);
            });
        });
}
