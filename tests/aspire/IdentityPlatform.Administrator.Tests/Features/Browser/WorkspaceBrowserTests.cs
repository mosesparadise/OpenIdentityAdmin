using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using IdentityPlatform.Issuer.Tests.Features.Browser;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Exercises the embedded React workspace against genuine HTTPS/password/MFA/provider authority and explicit display-only failures.</summary>
[Collection("administrator-bootstrap")]
[Trait("Category", "Browser")]
[Trait("BrowserStory", "BrowserProtection")]
public sealed class WorkspaceBrowserTests(BootstrapFixture fixture)
{
    private const string ReviewSession = "Review your session";
    private const string Appearance = "Appearance";
    private const string Tab = "Tab";
    private const string Enter = "Enter";
    private const string SignIn = "Sign in";
    private const string StartSignIn = "Start sign-in";
    private const string LoginHeading = "Administrator sign-in";
    private const string ReturnLink = "#workspace-return";
    private const string ViewSession = "View session";
    private const string StatusPath = "/bff/session";
    private const string KeyboardFocus = "() => document.activeElement?.textContent?.includes('Sign in') === true && parseFloat(getComputedStyle(document.activeElement).outlineWidth) >= 3";
    private const string Visibility = "() => document.dispatchEvent(new Event('visibilitychange'))";

    private static readonly string[] InvalidResponses = ["{", "{\"authenticated\":true}", "{\"authenticated\":false,\"subject\":\"stale\"}"];
    private const int SuccessStatus = 200;
    private static readonly bool[] ScriptCapabilities = [true, false];
    private const int ClientReturns = 2;
    private const int NativeRedirects = 3;
    private const int IssuerForms = 2;
    private const string StorageDenied = "Object.defineProperty(window,'localStorage',{get(){throw new DOMException('Unavailable','SecurityError')}})";
    private const string DarkTheme = "dark";
    private const string Html = "html";
    private const string ThemeAttribute = "data-theme";
    private const string NoScript = "noscript";
    private const string Detail = "/session";
    private const string Status = "**/bff/session";
    private const string SignedIn = "You’re signed in";
    private const string SignedOut = "Not signed in";
    private const string Unavailable = "We couldn’t check your session";
    private const string Expired = "Your last checked session has expired";
    private const string Check = "Check session";
    private const string Again = "Check again";
    private const string Identity = ".account-id";
    private const string Json = "application/json";
    private const string PageHide = "() => window.dispatchEvent(new PageTransitionEvent('pagehide', { persisted: true }))";
    private const string PageShow = "() => window.dispatchEvent(new PageTransitionEvent('pageshow', { persisted: true }))";

    /// <summary>Both palettes, system preference, navigation, keyboard focus and responsive accessibility operate on actual shipped resources.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_AnonymousWorkspace_When_AppearanceAndNavigationChange_Then_AccessibleResponsiveViewsRemainUsable(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await page.GotoAsync(environment.Profile.ClientUri.AbsoluteUri);
            await HeadingAsync(page, SignedOut);
            await WorkspacePresentation.RequireAsync(page, provider);
            await page.GetByRole(AriaRole.Link, new()
            {
                Name = ReviewSession,
                Exact = true
            }).ClickAsync();
            await HeadingAsync(page, SignedOut);
            Assert.True(page.Url == new Uri(environment.Profile.ClientUri, Detail).AbsoluteUri);
            await page.GetByRole(AriaRole.Combobox, new()
            {
                Name = Appearance
            }).FocusAsync();
            await page.Keyboard.PressAsync(Tab);
            Assert.True(await page.EvaluateAsync<bool>(KeyboardFocus));
            await page.Keyboard.PressAsync(Enter);
            await HeadingAsync(page, LoginHeading);
            await page.Locator(ReturnLink).ClickAsync();
            await HeadingAsync(page, SignedOut);
            Assert.Equal(0, environment.ParRequests);
            Assert.Equal(0, environment.TokenRequests);
        });

    /// <summary>Real login returns to the shell; read failures erase identity, recovery preserves rows/deadline, and original expiry does not renew.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_GenuineWorkspaceSession_When_ReadFailsAndOriginalDeadlineArrives_Then_IdentityClearsWithoutRenewal(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            var probe = new BrowserStatusReadProbe(true, () => { });
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, probe.Configure, workspace: true);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await SignInAsync(environment, page);
            await page.GetByRole(AriaRole.Link, new()
            {
                Name = ViewSession,
                Exact = true
            }).ClickAsync();
            await HeadingAsync(page, SignedIn);
            await WorkspacePresentation.RequireStorageAsync(page);
            Assert.True(await page.Locator(Identity).TextContentAsync() == environment.Issuer.Account.ActorId);
            await WorkspacePresentation.RequireStateAsync(page);
            var before = await BrowserHttpsState.SnapshotAsync(environment);
            var expiry = await ExpiryAsync(environment);
            probe.Armed = true;
            var denied = await page.RunAndWaitForResponseAsync(() => Button(page, Check).ClickAsync(), response => response.Url.EndsWith(StatusPath, StringComparison.Ordinal));
            Assert.Equal(503, denied.Status);
            await HeadingAsync(page, Unavailable);
            await WorkspacePresentation.RequireStorageAsync(page);
            await WorkspacePresentation.RequireStateAsync(page);
            Assert.Equal(0, await page.Locator(Identity).CountAsync());
            await Button(page, Again).ClickAsync();
            await HeadingAsync(page, SignedIn);
            await WorkspacePresentation.RequireStorageAsync(page);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            await ExpireAsync(environment, page, expiry);
            Assert.Equal(before, await BrowserHttpsState.SnapshotAsync(environment));
            Assert.Equal(1, environment.ParRequests);
            Assert.Equal(1, environment.TokenRequests);
            environment.RequirePolicies(new(1, ClientReturns, 1, NativeRedirects), new(IssuerForms, 1, IssuerForms, NativeRedirects));
        });

    /// <summary>Adversarial response-only substitutions never establish server authority; offline/malformed observations and lifecycle suspension remove display identity.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_ObservedWorkspace_When_TransportOrPageLifecycleChanges_Then_StaleIdentityIsNeverRetained(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true);
            await using var session = await BrowserSession.CreateAsync();
            var page = await session.Context.NewPageAsync();
            await SignInAsync(environment, page);
            await page.GotoAsync(new Uri(environment.Profile.ClientUri, Detail).AbsoluteUri);
            await HeadingAsync(page, SignedIn);
            await WorkspacePresentation.RequireStorageAsync(page);
            await MalformedAsync(page);
            await session.Context.SetOfflineAsync(true);
            await Button(page, Check).ClickAsync();
            await HeadingAsync(page, Unavailable);
            await WorkspacePresentation.RequireStorageAsync(page);
            Assert.Equal(0, await page.Locator(Identity).CountAsync());
            await session.Context.SetOfflineAsync(false);
            await Button(page, Again).ClickAsync();
            await HeadingAsync(page, SignedIn);
            await WorkspacePresentation.RequireStorageAsync(page);
            await page.EvaluateAsync(PageHide);
            Assert.Equal(0, await page.Locator(Identity).CountAsync());
            await page.EvaluateAsync(PageShow);
            await HeadingAsync(page, SignedIn);
            await WorkspacePresentation.RequireStorageAsync(page);
            await BrowserHttpsJourney.LogoutAsync(environment, page, issuer: false);
            await page.Locator(ReturnLink).ClickAsync();
            await HeadingAsync(page, SignedOut);
            await WorkspacePresentation.RequireStorageAsync(page);
            Assert.Equal(1, environment.TokenRequests);
        });

    /// <summary>Storage refusal leaves the shell usable, and JavaScript-disabled browsers retain real native sign-in links.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public Task Given_BrowserCapabilitiesUnavailable_When_WorkspaceOpens_Then_ExplicitNativeNavigationRemainsUsable(string provider) =>
        AdministratorBrowserEvidence.RunAsync(async () =>
        {
            await using var environment = await BrowserHttpsFixture.CreateAsync(fixture, provider, workspace: true);
            foreach (var script in ScriptCapabilities)
            {
                await using var session = await BrowserSession.CreateAsync(javaScript: script);
                if (script)
                {
                    await session.Context.AddInitScriptAsync(StorageDenied);
                }
                var page = await session.Context.NewPageAsync();
                await page.GotoAsync(environment.Profile.ClientUri.AbsoluteUri);
                if (script)
                {
                    await HeadingAsync(page, SignedOut);
                    await page.GetByRole(AriaRole.Combobox, new()
                    {
                        Name = Appearance
                    }).SelectOptionAsync(DarkTheme);
                    Assert.Equal("dark", await page.Locator(Html).GetAttributeAsync(ThemeAttribute));
                }
                else
                {
                    Assert.True(await page.Locator(NoScript).IsVisibleAsync());
                }
                await page.GetByRole(AriaRole.Link, new()
                {
                    Name = SignIn,
                    Exact = true
                }).ClickAsync();
                await HeadingAsync(page, LoginHeading);
            }
            Assert.Equal(0, environment.TokenRequests);
        });

    /// <summary>Starts at the user-visible shell and completes real native forms, preserving the fixed callback landing.</summary>
    private static async Task SignInAsync(BrowserHttpsFixture environment, IPage page)
    {
        await page.GotoAsync(environment.Profile.ClientUri.AbsoluteUri);
        await HeadingAsync(page, SignedOut);
        await page.GetByRole(AriaRole.Link, new()
        {
            Name = SignIn,
            Exact = true
        }).ClickAsync();
        await Button(page, StartSignIn).ClickAsync();
        await BrowserHttpsJourney.PasswordAsync(environment, page);
        await BrowserHttpsJourney.FactorAsync(environment, page);
        await HeadingAsync(page, SignedIn);
        await WorkspacePresentation.RequireStorageAsync(page);
        Assert.True(page.Url == environment.Profile.ClientUri.AbsoluteUri);
    }

    /// <summary>Checks local observed expiry first, then authoritative equality at the original server deadline.</summary>
    private static async Task ExpireAsync(BrowserHttpsFixture environment, IPage page, long expiry)
    {
        await page.Clock.SetFixedTimeAsync(new DateTime(expiry, DateTimeKind.Utc));
        await page.EvaluateAsync(Visibility);
        await HeadingAsync(page, Expired);
        Assert.Equal(0, await page.Locator(Identity).CountAsync());
        environment.Issuer.Account.Journey.Clock.Now = new DateTimeOffset(expiry, TimeSpan.Zero);
        await Button(page, Check).ClickAsync();
        await HeadingAsync(page, SignedOut);
    }

    /// <summary>Substitutes only responses after real authentication and requires explicit restoration after each malformed observation.</summary>
    private static async Task MalformedAsync(IPage page)
    {
        foreach (var body in InvalidResponses)
        {
            await page.RouteAsync(Status, route => route.FulfillAsync(new() { Status = SuccessStatus, ContentType = Json, Body = body }));
            await Button(page, Check).ClickAsync();
            await HeadingAsync(page, Unavailable);
            await WorkspacePresentation.RequireStorageAsync(page);
            Assert.Equal(0, await page.Locator(Identity).CountAsync());
            await page.UnrouteAsync(Status);
            await Button(page, Again).ClickAsync();
            await HeadingAsync(page, SignedIn);
            await WorkspacePresentation.RequireStorageAsync(page);
        }
    }

    /// <summary>Reads the original durable deadline independently of the browser's formatted observation.</summary>
    private static async Task<long> ExpiryAsync(BrowserHttpsFixture environment)
    {
        await using var scope = environment.Client.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>().Set<AdminSessionTicket>()
            .Select(row => row.IdleExpiresAt).SingleAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Waits for exact user-visible state without exposing account values in diagnostics.</summary>
    private static Task HeadingAsync(IPage page, string text) => page.GetByRole(AriaRole.Heading, new() { Name = text, Exact = true }).WaitForAsync();
    /// <summary>Resolves an explicitly named accessible action in the live React tree.</summary>
    private static ILocator Button(IPage page, string text) => page.GetByRole(AriaRole.Button, new() { Name = text, Exact = true });
}
