using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Checks actual stale forms and native back/reload/forward after a known local logout commit.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class SessionHistoryBrowserTests(BootstrapFixture fixture)
{
    private const string Local = "button[name=Mode][value=local]";

    /// <summary>An old real logout tab cannot repeat mutation, and history navigation cannot restore the revoked ticket or expose its protected cookie.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_CommittedLocalLogout_When_StaleTabAndBrowserHistoryAreUsed_Then_OldAuthorityCannotReturn(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await BrowserHttpsJourney.SignInAsync(environment, page);
            var cookies = await BrowserHttpsJourney.CookiesAsync(environment, page);
            var stale = await session.Context.NewPageAsync();
            await stale.GotoAsync(new Uri(environment.Profile.ClientUri, BrowserHttpsJourney.Logout).AbsoluteUri);
            await BrowserHttpsJourney.LogoutAsync(environment, page, false);
            var before = await BrowserHttpsState.SnapshotAsync(environment);
            var denied = await stale.RunAndWaitForResponseAsync(() => stale.Locator(Local).ClickAsync(), response => response.Request.Method == HttpMethods.Post);
            Assert.Equal(StatusCodes.Status400BadRequest, denied.Status);
            await AdministratorHistory.ObserveAsync(page, cookies[0].Value, () => page.GoBackAsync());
            await AdministratorHistory.ObserveAsync(page, cookies[0].Value, () => page.ReloadAsync());
            await AdministratorHistory.ObserveAsync(page, cookies[0].Value, () => page.GoForwardAsync());
            await BrowserHttpsJourney.RequireStatusAsync(environment, page, false);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            Assert.Equal(1, environment.TokenRequests);
        });
}
