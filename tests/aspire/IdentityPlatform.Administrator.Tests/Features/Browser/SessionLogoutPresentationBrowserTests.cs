using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Exercises actual light/dark, responsive and keyboard-safe logout outcomes while retaining native authentication and honest partial-failure semantics.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserSessions")]
public sealed class SessionLogoutPresentationBrowserTests(BootstrapFixture fixture)
{
    private static readonly BrowserHttpsCounts ClientCounts = new(5, 5, 9, 6);
    private static readonly BrowserHttpsCounts IssuerCounts = new(5, 2, 7, 7);
    private const string LocallyEnded = "session has ended locally";
    private const string Main = "main";
    private const string Local = "button[name=Mode][value=local]";
    private const string Issuer = "button[name=Mode][value=issuer]";
    private const string Confirm = "button[type=submit]";
    private const string Hint = "logout_hint";
    private const string ErrorPath = "/bff/login/error";
    private const string IncompleteTitle = "Issuer sign-out not confirmed";

    /// <summary>Real form/result documents preserve independently stored themes, responsive semantics and keyboard actions across rollback acknowledgement and issuer return.</summary>
    [Theory]
    [InlineData("PostgreSql", false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, Traits = ["Provider", "SqlServer"])]
    public Task Given_RealBrowserDocuments_When_ThemeViewportAndKeyboardChange_Then_LogoutOutcomesStayAccessibleAndTruthful(string provider, bool dark) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            var fault = new BrowserRemoteLogoutFault(BrowserMutation.Logout) { LoseAcknowledgement = true };
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, fault.Configure);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await page.EmulateMediaAsync(new()
            {
                ColorScheme = dark ? ColorScheme.Dark : ColorScheme.Light
            });
            await page.GotoAsync(new Uri(environment.Profile.ClientUri, BrowserHttpsJourney.Login).AbsoluteUri);
            await BrowserLogoutPresentation.InitializeAsync(page, dark);
            await InspectAsync(page, provider, dark, BrowserLogoutPresentation.Screen.Login);
            await BrowserHttpsJourney.SignInAsync(environment, page);
            var oldSource = await BrowserHttpsEffects.SourceAsync(environment);
            await LocalAsync(environment, page, fault, provider, dark);
            await BrowserHttpsEffects.LocalAsync(environment, oldSource, null);
            await RemoteAsync(environment, page, oldSource, provider, dark);
            await page.GotoAsync(new Uri(environment.Profile.ClientUri, ErrorPath).AbsoluteUri);
            await InspectAsync(page, provider, dark, BrowserLogoutPresentation.Screen.Error);
            Assert.Equal(2, environment.ParRequests);
            Assert.Equal(2, environment.TokenRequests);
            Assert.Equal(4, environment.EndSessionGets);
            Assert.Equal(1, environment.EndSessionPosts);
            environment.RequirePolicies(ClientCounts, IssuerCounts);
        });

    /// <summary>Keyboard-submitted committed acknowledgement loss stays visibly unknown; explicit reconciliation observes the same durable logout.</summary>
    private static async Task LocalAsync(BrowserHttpsFixture environment, IPage page, BrowserRemoteLogoutFault fault, string provider, bool dark)
    {
        await page.GotoAsync(new Uri(environment.Profile.ClientUri, BrowserHttpsJourney.Logout).AbsoluteUri);
        await InspectAsync(page, provider, dark, BrowserLogoutPresentation.Screen.Logout);
        var response = await page.RunAndWaitForResponseAsync(() => BrowserLogoutPresentation.SubmitAsync(page, Local),
            response => response.Request.Method == HttpMethods.Post);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.Status);
        await page.Locator(Main).Filter(new()
        {
            HasText = BrowserLogoutJourney.Unknown
        }).WaitForAsync();
        Assert.Equal(1, fault.Commits);
        await InspectAsync(page, provider, dark, BrowserLogoutPresentation.Screen.Unknown);
        fault.LoseAcknowledgement = false;
        var before = await BrowserHttpsState.SnapshotAsync(environment);
        await page.GotoAsync(new Uri(environment.Profile.ClientUri, BrowserHttpsJourney.Logout).AbsoluteUri);
        await BrowserLogoutPresentation.SubmitAsync(page, Local);
        await page.Locator(Main).Filter(new()
        {
            HasText = LocallyEnded
        }).WaitForAsync();
        Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
        Assert.Equal(1, fault.Commits);
        await InspectAsync(page, provider, dark, BrowserLogoutPresentation.Screen.Local);
    }

    /// <summary>A later fresh ceremony and explicit native confirmation retain both origins' independent theme storage and exact issuer effects.</summary>
    private static async Task RemoteAsync(BrowserHttpsFixture environment, IPage page, string oldSource, string provider, bool dark)
    {
        await BrowserHttpsJourney.SignInAsync(environment, page);
        var source = await BrowserHttpsEffects.SourceAsync(environment);
        Assert.True(source != oldSource);
        await page.GotoAsync(new Uri(environment.Profile.ClientUri, BrowserHttpsJourney.Logout).AbsoluteUri);
        await BrowserLogoutPresentation.RequireAsync(page, dark);
        await BrowserLogoutPresentation.SubmitAsync(page, Issuer, second: true);
        var link = await BrowserLogoutJourney.LinkAsync(environment, page);
        await page.GotoAsync(link.AbsoluteUri);
        await BrowserLogoutPresentation.InitializeAsync(page, !dark, initialDark: dark);
        await InspectAsync(page, provider, !dark, BrowserLogoutPresentation.Screen.Issuer);
        var fields = QueryHelpers.ParseQuery(link.Query).ToDictionary(item => item.Key, item => (string?)item.Value.ToString(), StringComparer.Ordinal);
        fields[Hint] = oldSource;
        await BrowserLogoutJourney.DeniedAsync(page, new Uri(QueryHelpers.AddQueryString(link.GetLeftPart(UriPartial.Path), fields)));
        Assert.True(await page.TitleAsync() == IncompleteTitle);
        await InspectAsync(page, provider, !dark, BrowserLogoutPresentation.Screen.Incomplete);
        await page.GotoAsync(link.AbsoluteUri);
        await BrowserLogoutPresentation.SubmitAsync(page, Confirm);
        await BrowserLogoutJourney.RequireReturnedAsync(environment, page);
        await BrowserHttpsState.ReturnedAsync(environment, source);
        await BrowserHttpsEffects.IssuerAsync(environment, source, revoked: true);
        await InspectAsync(page, provider, dark, BrowserLogoutPresentation.Screen.Returned);
        await BrowserHttpsJourney.RequireStatusAsync(environment, page, false);
    }

    /// <summary>Captures only reviewed fixed-content documents after actual semantic, computed-color and axe assertions pass.</summary>
    private static async Task InspectAsync(IPage page, string provider, bool dark, BrowserLogoutPresentation.Screen screen)
    {
        await BrowserLogoutPresentation.RequireAsync(page, dark);
        await BrowserLogoutPresentation.CaptureAsync(page, provider, dark, screen);
    }
}
